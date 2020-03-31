using IXICore;
using IXICore.Meta;
using IXICore.Network;
using IXICore.Utils;
using SpixiBot.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SpixiBot.Network
{
    class StreamTransaction
    {
        public string messageID;
        public Transaction transaction;
    }

    class BotContact
    {
        public byte[] nickData;
        public byte[] publicKey;

        public BotContact()
        {

        }

        public BotContact(byte[] contact_bytes)
        {
            using (MemoryStream m = new MemoryStream(contact_bytes))
            {
                using (BinaryReader reader = new BinaryReader(m))
                {
                    int nd_length = reader.ReadInt32();
                    if (nd_length > 0)
                    {
                        nickData = reader.ReadBytes(nd_length);
                    }

                    int pk_length = reader.ReadInt32();
                    if (pk_length > 0)
                    {
                        publicKey = reader.ReadBytes(pk_length);
                    }
                }
            }
        }

        public byte[] getBytes()
        {
            using (MemoryStream m = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(m))
                {
                    if (nickData == null)
                    {
                        writer.Write((int)0);
                    }
                    else
                    {
                        writer.Write(nickData.Length);
                        writer.Write(nickData);
                    }

                    if (publicKey == null)
                    {
                        writer.Write((int)0);
                    }
                    else
                    {
                        writer.Write(publicKey.Length);
                        writer.Write(publicKey);
                    }
                }
                return m.ToArray();
            }
        }
    }


    class StreamProcessor
    {
        static List<StreamMessage> messages = new List<StreamMessage>(); // List that stores stream messages
        static List<StreamTransaction> transactions = new List<StreamTransaction>(); // List that stores stream transactions
        static Dictionary<byte[], BotContact> contacts = new Dictionary<byte[], BotContact>(new ByteArrayComparer());

        static string avatarPath = "Avatars";
        static string contactsPath = "contacts.dat";
        static string messagesPath = "messages.dat";

        public static void init(string base_path = "", string avatar_path = "Avatars")
        {
            avatarPath = Path.Combine(base_path, avatar_path);
            if (!Directory.Exists(avatarPath))
            {
                Directory.CreateDirectory(avatarPath);
            }

            contactsPath = Path.Combine(base_path, contactsPath);
            messagesPath = Path.Combine(base_path, messagesPath);

            loadContactsFromFile();
            loadMessagesFromFile();
        }

        // Called when receiving S2 data from clients
        public static void receiveData(byte[] bytes, RemoteEndpoint endpoint)
        {
            string endpoint_wallet_string = Base58Check.Base58CheckEncoding.EncodePlain(endpoint.presence.wallet);
            Logging.info(string.Format("Receiving S2 data from {0}", endpoint_wallet_string));

            StreamMessage message = new StreamMessage(bytes);

            // Don't allow clients to send error stream messages, as it's reserved for S2 nodes only
            if(message.type == StreamMessageCode.error)
            {
                Logging.warn(string.Format("Discarding error message type from {0}", endpoint_wallet_string));
                return;
            }

            // TODO: commented for development purposes ONLY!
            /*if (QuotaManager.exceededQuota(endpoint.presence.wallet))
            {
                Logging.error(string.Format("Exceeded quota of info relay messages for {0}", endpoint_wallet_string));
                sendError(endpoint.presence.wallet);
                return;
            }*/

            bool data_message = false;
            if (message.type == StreamMessageCode.data)
                data_message = true;

            // Discard messages not sent to this node
            if(!IxianHandler.getWalletStorage().isMyAddress(message.recipient))
            {
                Logging.warn(string.Format("Discarding message that wasn't sent to this node from {0}", endpoint_wallet_string));
                return;
            }

            if (message.encryptionType != StreamMessageEncryptionCode.none && !message.decrypt(IxianHandler.getWalletStorage().getPrimaryPrivateKey(), null, null))
            {
                Logging.error("Could not decrypt message from {0}", Base58Check.Base58CheckEncoding.EncodePlain(message.sender));
                return;
            }

            SpixiMessage spixi_msg = new SpixiMessage(message.data);

            switch(spixi_msg.type)
            {
                case SpixiMessageCode.requestAdd:
                    // Friend request
                    if (!new Address(spixi_msg.data).address.SequenceEqual(message.sender) || !message.verifySignature(spixi_msg.data))
                    {
                        Logging.error("Unable to verify signature for message type: {0}, id: {1}, from: {2}.", message.type, Crypto.hashToString(message.id), Base58Check.Base58CheckEncoding.EncodePlain(message.sender));
                    }
                    else
                    {
                        sendAcceptAdd(endpoint.presence.wallet);
                    }
                    break;

                case SpixiMessageCode.getPubKey:
                    if (contacts.ContainsKey(spixi_msg.data))
                    {
                        StreamMessage sm = new StreamMessage();
                        sm.type = StreamMessageCode.info;
                        sm.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
                        sm.recipient = message.sender;
                        sm.data = new SpixiMessage(SpixiMessageCode.pubKey, contacts[spixi_msg.data].publicKey).getBytes();
                        sm.transaction = new byte[1];
                        sm.sigdata = new byte[1];
                        sm.encryptionType = StreamMessageEncryptionCode.none;

                        sendMessage(endpoint.presence.wallet, sm);
                    }
                    break;

                case SpixiMessageCode.getNick:
                    sendNickname(endpoint.presence.wallet, spixi_msg.data);
                    break;

                case SpixiMessageCode.getAvatar:
                    sendAvatar(endpoint.presence.wallet, spixi_msg.data);
                    break;

                case SpixiMessageCode.nick:
                    lock (contacts)
                    {
                        if (!contacts.ContainsKey(endpoint.presence.wallet))
                        {
                            contacts.Add(endpoint.presence.wallet, new BotContact() { publicKey = endpoint.serverPubKey });
                        }
                        contacts[endpoint.presence.wallet].nickData = message.getBytes();
                    }
                    writeContactsToFile();
                    break;

                case SpixiMessageCode.avatar:
                    lock (contacts)
                    {
                        if (!contacts.ContainsKey(endpoint.presence.wallet))
                        {
                            contacts.Add(endpoint.presence.wallet, new BotContact() { publicKey = endpoint.serverPubKey });
                        }
                        string path = Path.Combine(avatarPath, Base58Check.Base58CheckEncoding.EncodePlain(endpoint.presence.wallet) + ".raw");
                        File.WriteAllBytes(path, message.getBytes());
                    }
                    writeContactsToFile();
                    break;

                case SpixiMessageCode.chat:
                    if (!contacts.ContainsKey(endpoint.presence.wallet) || contacts[endpoint.presence.wallet].nickData == null)
                    {
                        requestNickname(endpoint.presence.wallet);
                        requestAvatar(endpoint.presence.wallet);
                    }

                    lock (messages)
                    {
                        if (messages.Find(x => x.id.SequenceEqual(message.id)) == null)
                        {
                            messages.Add(message);
                            if (messages.Count > 1000)
                            {
                                messages.RemoveAt(0);
                            }
                            QuotaManager.addActivity(endpoint.presence.wallet, data_message);

                            // Relay certain messages without transaction
                            NetworkServer.forwardMessage(ProtocolMessageCode.s2data, bytes, endpoint.presence.wallet);
                        }
                    }
                    writeMessagesToFile();
                    break;

                case SpixiMessageCode.getMessages:
                    sendMessages(endpoint.presence.wallet, spixi_msg.data);
                    break;

                case SpixiMessageCode.msgReceived:
                    {
                        // don't send confirmation back, so just return
                        return;
                    }

                case SpixiMessageCode.msgRead:
                    {
                        // don't send confirmation back, so just return
                        return;
                    }

                default:
                    Logging.warn("Received message type that isn't handled {0}", spixi_msg.type);
                    break;
            }

            // Send received confirmation
            StreamMessage msg_received = new StreamMessage();
            msg_received.type = StreamMessageCode.info;
            msg_received.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
            msg_received.recipient = message.sender;
            msg_received.data = new SpixiMessage(SpixiMessageCode.msgReceived, message.id).getBytes();
            msg_received.transaction = new byte[1];
            msg_received.sigdata = new byte[1];
            msg_received.encryptionType = StreamMessageEncryptionCode.none;

            sendMessage(endpoint.presence.wallet, msg_received);


            // TODO: commented for development purposes ONLY!
            /*
                        // Extract the transaction
                        Transaction transaction = new Transaction(message.transaction);

                        // Validate transaction sender
                        if(transaction.from.SequenceEqual(message.sender) == false)
                        {
                            Logging.error(string.Format("Relayed message transaction mismatch for {0}", endpoint_wallet_string));
                            sendError(message.sender);
                            return;
                        }

                        // Validate transaction amount and fee
                        if(transaction.amount < CoreConfig.relayPriceInitial || transaction.fee < CoreConfig.transactionPrice)
                        {
                            Logging.error(string.Format("Relayed message transaction amount too low for {0}", endpoint_wallet_string));
                            sendError(message.sender);
                            return;
                        }

                        // Validate transaction receiver
                        if (transaction.toList.Keys.First().SequenceEqual(Node.walletStorage.address) == false)
                        {
                            Logging.error("Relayed message transaction receiver is not this S2 node");
                            sendError(message.sender);
                            return;
                        }

                        // Update the recipient dictionary
                        if (dataRelays.ContainsKey(message.recipient))
                        {
                            dataRelays[message.recipient]++;
                            if(dataRelays[message.recipient] > Config.relayDataMessageQuota)
                            {
                                Logging.error(string.Format("Exceeded amount of unpaid data relay messages for {0}", endpoint_wallet_string));
                                sendError(message.sender);
                                return;
                            }
                        }
                        else
                        {
                            dataRelays.Add(message.recipient, 1);
                        }


                        // Store the transaction
                        StreamTransaction streamTransaction = new StreamTransaction();
                        streamTransaction.messageID = message.getID();
                        streamTransaction.transaction = transaction;
                        lock (transactions)
                        {
                            transactions.Add(streamTransaction);
                        }

                        // For testing purposes, allow the S2 node to receive relay data itself
                        if (message.recipient.SequenceEqual(Node.walletStorage.getWalletAddress()))
                        {               
                            string test = Encoding.UTF8.GetString(message.data);
                            Logging.info(test);

                            return;
                        }

                        Logging.info("NET: Forwarding S2 data");
                        NetworkStreamServer.forwardMessage(message.recipient, DLT.Network.ProtocolMessageCode.s2data, bytes);      
                        */
        }

        // Called when receiving a transaction signature from a client
        public static void receivedTransactionSignature(byte[] bytes, RemoteEndpoint endpoint)
        {
            using (MemoryStream m = new MemoryStream(bytes))
            {
                using (BinaryReader reader = new BinaryReader(m))
                {
                    // Read the message ID
                    string messageID = reader.ReadString();
                    int sig_length = reader.ReadInt32();
                    if(sig_length <= 0)
                    {
                        Logging.warn("Incorrect signature length received.");
                        return;
                    }

                    // Read the signature
                    byte[] signature = reader.ReadBytes(sig_length);

                    lock (transactions)
                    {
                        // Find the transaction with a matching message id
                        StreamTransaction tx = transactions.Find(x => x.messageID.Equals(messageID, StringComparison.Ordinal));
                        if(tx == null)
                        {
                            Logging.warn("No transaction found to match signature messageID.");
                            return;
                        }
                     
                        // Compose a new transaction and apply the received signature
                        Transaction transaction = new Transaction(tx.transaction);
                        transaction.signature = signature;

                        // Verify the signed transaction
                        if (transaction.verifySignature(transaction.pubKey, null))
                        {
                            // Broadcast the transaction
                            CoreProtocolMessage.broadcastProtocolMessage(new char[] { 'M', 'H' }, ProtocolMessageCode.newTransaction, transaction.getBytes(), null, endpoint);
                        }
                        return;
                                                 
                    }
                }
            }
        }


        // Called periodically to clear the black list
        public static void update()
        {

        }

        // Sends an error stream message to a recipient
        // TODO: add additional data for error details
        public static void sendError(byte[] recipient)
        {
            StreamMessage message = new StreamMessage();
            message.type = StreamMessageCode.error;
            message.recipient = recipient;
            message.transaction = new byte[1];
            message.sigdata = new byte[1];
            message.data = new byte[1];

            NetworkServer.forwardMessage(recipient, ProtocolMessageCode.s2data, message.getBytes());
        }


        // Send an encrypted message using the S2 network
        public static bool sendMessage(byte[] recipient_address, StreamMessage msg)
        {
            /*byte[] pubkey = null;

            Presence p = PresenceList.getPresenceByAddress(recipient_address);
            if (p != null && p.addresses.Find(x => x.type == 'C') != null)
            {
                pubkey = p.pubkey;
            }
            msg.encrypt(pubkey, null, null);*/

            NetworkServer.forwardMessage(recipient_address, ProtocolMessageCode.s2data, msg.getBytes());

            return true;

            /*         string pub_k = FriendList.findContactPubkey(msg.recipientAddress);
                     if (pub_k.Length < 1)
                     {
                         Console.WriteLine("Contact {0} not found, adding to offline queue!", msg.recipientAddress);
                         addOfflineMessage(msg);
                         return;
                     }


                     // Create a new IXIAN transaction
                     //  byte[] checksum = Crypto.sha256(encrypted_message);
                     Transaction transaction = new Transaction(0, msg.recipientAddress, Node.walletStorage.address);
                     //  transaction.data = Encoding.UTF8.GetString(checksum);
                     msg.transactionID = transaction.id;
                     //ProtocolMessage.broadcastProtocolMessage(ProtocolMessageCode.newTransaction, transaction.getBytes());

                     // Add message to the queue
                     messages.Add(msg);

                     // Request a new keypair from the S2 Node
                     if(hostname == null)
                         ProtocolMessage.broadcastProtocolMessage(ProtocolMessageCode.s2generateKeys, Encoding.UTF8.GetBytes(msg.getID()));
                     else
                     {
                         NetworkClientManager.sendData(ProtocolMessageCode.s2generateKeys, Encoding.UTF8.GetBytes(msg.getID()), hostname);
                     }*/
        }

        public static void sendAcceptAdd(byte[] recipient)
        {
            SpixiMessage spixi_message = new SpixiMessage(SpixiMessageCode.acceptAddBot, null);

            StreamMessage message = new StreamMessage();
            message.type = StreamMessageCode.info;
            message.recipient = recipient;
            message.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
            message.transaction = new byte[1];
            message.sigdata = new byte[1];
            message.data = spixi_message.getBytes();
            message.encryptionType = StreamMessageEncryptionCode.none;
            message.id = new byte[] { 1 };

            message.sign(IxianHandler.getWalletStorage().getPrimaryPrivateKey());

            sendMessage(recipient, message);
        }

        public static void sendNickname(byte[] recipient, byte[] contact_address)
        {
            SpixiMessage reply_spixi_message = null;
            byte[] sender = null;
            byte[] tmp_recipient = recipient;
            if (contact_address != null && contact_address.Length > 1)
            {
                if (!contacts.ContainsKey(contact_address))
                {
                    return;
                }

                sendMessage(recipient, new StreamMessage(contacts[contact_address].nickData));

                return;
            }

            reply_spixi_message = new SpixiMessage(SpixiMessageCode.nick, Encoding.UTF8.GetBytes(Config.botName));
            sender = IxianHandler.getWalletStorage().getPrimaryAddress();

            // Send the nickname message to friend
            StreamMessage reply_message = new StreamMessage();
            reply_message.type = StreamMessageCode.info;
            reply_message.recipient = tmp_recipient;
            reply_message.sender = sender;
            reply_message.transaction = new byte[1];
            reply_message.sigdata = new byte[1];
            reply_message.data = reply_spixi_message.getBytes();
            reply_message.encryptionType = StreamMessageEncryptionCode.none;
            reply_message.id = new byte[] { 4 };

            reply_message.sign(IxianHandler.getWalletStorage().getPrimaryPrivateKey());

            sendMessage(recipient, reply_message);
        }

        public static void sendAvatar(byte[] recipient, byte[] contact_address)
        {
            SpixiMessage reply_spixi_message = null;
            byte[] sender = null;
            byte[] tmp_recipient = recipient;
            if (contact_address != null && contact_address.Length > 1)
            {
                lock (contacts)
                {
                    if (!contacts.ContainsKey(contact_address))
                    {
                        return;
                    }

                    string path = Path.Combine(avatarPath, Base58Check.Base58CheckEncoding.EncodePlain(contact_address) + ".raw");
                    if (!File.Exists(path))
                    {
                        return;
                    }

                    byte[] avatar_data = File.ReadAllBytes(path);
                    if (avatar_data != null)
                    {
                        sendMessage(recipient, new StreamMessage(avatar_data));
                    }
                }

                return;
            }

            byte[] avatar_bytes = Node.getAvatarBytes();

            if (avatar_bytes == null)
            {
                return;
            }

            reply_spixi_message = new SpixiMessage(SpixiMessageCode.avatar, avatar_bytes);
            sender = IxianHandler.getWalletStorage().getPrimaryAddress();

            // Send the nickname message to friend
            StreamMessage reply_message = new StreamMessage();
            reply_message.type = StreamMessageCode.info;
            reply_message.recipient = tmp_recipient;
            reply_message.sender = sender;
            reply_message.transaction = new byte[1];
            reply_message.sigdata = new byte[1];
            reply_message.data = reply_spixi_message.getBytes();
            reply_message.encryptionType = StreamMessageEncryptionCode.none;
            reply_message.id = new byte[] { 5 };

            reply_message.sign(IxianHandler.getWalletStorage().getPrimaryPrivateKey());

            sendMessage(recipient, reply_message);
        }

        // Requests the nickname of the sender
        public static void requestNickname(byte[] recipient)
        {
            // Prepare the message and send to the S2 nodes
            SpixiMessage spixi_message = new SpixiMessage(SpixiMessageCode.getNick, new byte[1]);

            StreamMessage message = new StreamMessage();
            message.type = StreamMessageCode.info;
            message.recipient = recipient;
            message.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
            message.transaction = new byte[1];
            message.sigdata = new byte[1];
            message.data = spixi_message.getBytes();
            message.encryptionType = StreamMessageEncryptionCode.none;
            message.id = new byte[] { 3 };

            sendMessage(recipient, message);
        }

        // Requests the nickname of the sender
        public static void requestAvatar(byte[] recipient)
        {
            // Prepare the message and send to the S2 nodes
            SpixiMessage spixi_message = new SpixiMessage(SpixiMessageCode.getAvatar, new byte[1]);

            StreamMessage message = new StreamMessage();
            message.type = StreamMessageCode.info;
            message.recipient = recipient;
            message.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
            message.transaction = new byte[1];
            message.sigdata = new byte[1];
            message.data = spixi_message.getBytes();
            message.encryptionType = StreamMessageEncryptionCode.none;
            message.id = new byte[] { 4 };

            sendMessage(recipient, message);
        }

        public static void sendMessages(byte[] recipient_address, byte[] last_message_id)
        {
            lock (messages)
            {
                int last_msg_index = -1;
                if (last_message_id != null)
                {
                    last_msg_index = messages.FindLastIndex(x => x.id.SequenceEqual(last_message_id));
                }
                for (int i = last_msg_index + 1; i < messages.Count; i++)
                {
                    sendMessage(recipient_address, messages[i]);
                }
            }
        }

        public static void writeMessagesToFile()
        {
            lock (messages)
            {
                BinaryWriter writer;
                try
                {
                    // Prepare the file for writing
                    writer = new BinaryWriter(new FileStream(messagesPath, FileMode.Create));
                }
                catch (IOException e)
                {
                    Logging.error("Cannot create messages.dat file. {0}", e.Message);
                    return;
                }

                try
                {
                    int version = 0;
                    writer.Write(version);

                    int message_num = messages.Count;
                    writer.Write(message_num);

                    foreach (var message in messages)
                    {
                        byte[] msg_bytes = message.getBytes();
                        writer.Write(msg_bytes.Length);
                        writer.Write(msg_bytes);
                    }
                }
                catch (IOException e)
                {
                    Logging.error("Cannot write to messages.dat file. {0}", e.Message);
                }
                writer.Close();
            }
        }

        public static void loadMessagesFromFile()
        {
            if (File.Exists(messagesPath) == false)
            {
                return;
            }

            lock (messages)
            {
                BinaryReader reader;
                try
                {
                    reader = new BinaryReader(new FileStream(messagesPath, FileMode.Open));
                }
                catch (IOException e)
                {
                    Logging.error("Cannot open messages.dat file. {0}", e.Message);
                    return;
                }

                try
                {
                    int version = reader.ReadInt32();

                    int num_messages = reader.ReadInt32();
                    for (int i = 0; i < num_messages; i++)
                    {
                        int msg_len = reader.ReadInt32();
                        byte[] msg_bytes = reader.ReadBytes(msg_len);

                        messages.Add(new StreamMessage(msg_bytes));
                    }
                }
                catch (Exception e)
                {
                    Logging.error("Cannot read from messages.dat file. {0}", e.Message);
                    // TODO TODO notify the user or something like that
                }

                reader.Close();
            }
        }

        public static void writeContactsToFile()
        {
            lock (contacts)
            {
                BinaryWriter writer;
                try
                {
                    // Prepare the file for writing
                    writer = new BinaryWriter(new FileStream(contactsPath, FileMode.Create));
                }
                catch (IOException e)
                {
                    Logging.log(LogSeverity.error, String.Format("Cannot create contacts.dat file. {0}", e.Message));
                    return;
                }

                try
                {
                    int version = 0;
                    writer.Write(version);

                    int num_contacts = contacts.Count;
                    writer.Write(num_contacts);

                    foreach (var contact in contacts)
                    {
                        byte[] contact_bytes = contact.Value.getBytes();
                        writer.Write(contact_bytes.Length);
                        writer.Write(contact_bytes);
                    }
                }
                catch (IOException e)
                {
                    Logging.error("Cannot write to contacts.dat file. {0}", e.Message);
                }
                writer.Close();
            }
        }

        public static void loadContactsFromFile()
        {
            if (File.Exists(contactsPath) == false)
            {
                return;
            }

            lock (contacts)
            {
                BinaryReader reader;
                try
                {
                    reader = new BinaryReader(new FileStream(contactsPath, FileMode.Open));
                }
                catch (IOException e)
                {
                    Logging.error("Cannot open contacts.dat file. {0}", e.Message);
                    return;
                }

                try
                {
                    int version = reader.ReadInt32();

                    int num_contacts = reader.ReadInt32();
                    for (int i = 0; i < num_contacts; i++)
                    {
                        int contact_len = reader.ReadInt32();
                        byte[] contact_bytes = reader.ReadBytes(contact_len);

                        BotContact bc = new BotContact(contact_bytes);
                        byte[] address = new Address(bc.publicKey).address;
                        contacts.AddOrReplace(address, bc);
                    }
                }
                catch (Exception e)
                {
                    Logging.error("Cannot read from contacts.dat file. {0}", e.Message);
                    // TODO TODO notify the user or something like that
                }

                reader.Close();
            }
        }
    }
}
