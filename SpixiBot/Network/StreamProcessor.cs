using IXICore;
using IXICore.Meta;
using IXICore.Network;
using IXICore.SpixiBot;
using SpixiBot.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SpixiBot.Network
{
    class StreamProcessor
    {
        static List<StreamMessage> pendingMessages = new List<StreamMessage>(); // List that stores pending/unpaid stream messages

        public static void init(string base_path = "")
        {
            Messages.init(base_path);
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

            int channel = 0;
            if (spixi_msg != null)
            {
                channel = spixi_msg.channel;
            }

            switch (spixi_msg.type)
            {
                case SpixiMessageCode.requestAdd:
                    // Friend request
                    if (!new Address(spixi_msg.data).address.SequenceEqual(message.sender) || !message.verifySignature(spixi_msg.data))
                    {
                        Logging.error("Unable to verify signature for message type: {0}, id: {1}, from: {2}.", message.type, Crypto.hashToString(message.id), Base58Check.Base58CheckEncoding.EncodePlain(message.sender));
                    }
                    else
                    {
                        sendAcceptAdd(endpoint.presence.wallet, endpoint.presence.pubkey);
                        sendAvatar(endpoint.presence.wallet, null);
                    }
                    break;

                case SpixiMessageCode.getPubKey:
                    if (Node.users.hasUser(spixi_msg.data))
                    {
                        StreamMessage sm = new StreamMessage();
                        sm.type = StreamMessageCode.info;
                        sm.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
                        sm.recipient = message.sender;
                        sm.data = new SpixiMessage(SpixiMessageCode.pubKey, Node.users.getUser(spixi_msg.data).publicKey).getBytes();
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
                    Node.users.setPubKey(endpoint.presence.wallet, endpoint.serverPubKey);
                    Node.users.setNick(endpoint.presence.wallet, message.getBytes());
                    break;

                case SpixiMessageCode.avatar:
                    Node.users.setPubKey(endpoint.presence.wallet, endpoint.serverPubKey);
                    Node.users.setAvatar(endpoint.presence.wallet, message.getBytes());
                    break;

                case SpixiMessageCode.chat:
                    onChat(bytes, message, channel, endpoint);
                    break;

                case SpixiMessageCode.botGetMessages:
                    Messages.sendMessages(endpoint.presence.wallet, channel, spixi_msg.data);
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

                case SpixiMessageCode.botAction:
                    onBotAction(spixi_msg.data, endpoint);
                    break;

                case SpixiMessageCode.msgDelete:
                    onMsgDelete(spixi_msg.data, channel, endpoint);
                    break;

                case SpixiMessageCode.msgReaction:
                    onMsgReaction(message, spixi_msg.data, channel, endpoint);
                    break;

                case SpixiMessageCode.leave:
                    onLeave(message.sender);

                    break;

                default:
                    Logging.warn("Received message type that isn't handled {0}", spixi_msg.type);
                    break;
            }

            // Send received confirmation
            StreamMessage msg_received = new StreamMessage();
            msg_received.type = StreamMessageCode.info;
            msg_received.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
            msg_received.recipient = message.sender;
            msg_received.data = new SpixiMessage(SpixiMessageCode.msgReceived, message.id, channel).getBytes();
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

        public static void onLeave(byte[] sender)
        {
            if (Node.users.hasUser(sender))
            {
                var user = Node.users.getUser(sender);
                user.status = BotContactStatus.left;
                Node.users.writeContactsToFile();

                StreamMessage sm = new StreamMessage();
                sm.type = StreamMessageCode.info;
                sm.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
                sm.recipient = sender;
                sm.data = new SpixiMessage(SpixiMessageCode.leaveConfirmed, null).getBytes();
                sm.transaction = new byte[1];
                sm.sigdata = new byte[1];
                sm.encryptionType = StreamMessageEncryptionCode.none;

                sendMessage(sender, sm);
            }
        }

        public static void onMsgDelete(byte[] msg_id, int channel, RemoteEndpoint endpoint)
        {
            StreamMessage msg = Messages.getMessage(msg_id, channel);
            if (msg == null)
            {
                return;
            }

            if (isAdmin(endpoint.presence.wallet) || msg.sender.SequenceEqual(endpoint.presence.wallet))
            {
                Messages.removeMessage(msg_id, channel);
                broadcastMsgDelete(msg_id, channel);
            }
        }

        public static void onMsgReaction(StreamMessage reaction_msg, byte[] msg_reaction_data, int channel, RemoteEndpoint endpoint)
        {
            SpixiMessageReaction smr = new SpixiMessageReaction(msg_reaction_data);
            StreamMessage msg = Messages.getMessage(smr.msgId, channel);
            if (msg == null)
            {
                return;
            }

            Messages.addMessage(reaction_msg, channel);
            NetworkServer.forwardMessage(ProtocolMessageCode.s2data, reaction_msg.getBytes());
        }

        private static void broadcastMsgDelete(byte[] msg_id, int channel)
        {
            SpixiMessage spixi_message = new SpixiMessage(SpixiMessageCode.msgDelete, msg_id, channel);

            StreamMessage message = new StreamMessage();
            message.type = StreamMessageCode.info;
            message.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
            message.recipient = message.sender;
            message.transaction = new byte[1];
            message.sigdata = new byte[1];
            message.data = spixi_message.getBytes();
            message.encryptionType = StreamMessageEncryptionCode.none;

            message.sign(Node.walletStorage.getPrimaryPrivateKey());

            Messages.addMessage(message, channel);
            NetworkServer.forwardMessage(ProtocolMessageCode.s2data, message.getBytes());
        }

        public static bool isAdmin(byte[] contact_address)
        {
            int default_group = Int32.Parse(Node.settings.getOption("defaultGroup", "0"));

            int role_index = Node.users.getUser(contact_address).getPrimaryRole();
            BotGroup group;
            if (Node.groups.groupIndexToName(role_index) != "")
            {
                group = Node.groups.getGroup(Node.groups.groupIndexToName(role_index));
            }
            else
            {
                group = Node.groups.getGroup(Node.groups.groupIndexToName(default_group));
            }
            return group.admin;
        }

        public static IxiNumber getMessagePrice(byte[] contact_address, int msg_len)
        {
            int default_group = Int32.Parse(Node.settings.getOption("defaultGroup", "0"));

            int role_index = Node.users.getUser(contact_address).getPrimaryRole();
            BotGroup group;
            if (Node.groups.groupIndexToName(role_index) != "")
            {
                group = Node.groups.getGroup(Node.groups.groupIndexToName(role_index));
            }
            else
            {
                group = Node.groups.getGroup(Node.groups.groupIndexToName(default_group));
            }
            return group.messageCost * msg_len / 1000;
        }

        public static void onChat(byte[] raw_message, StreamMessage message, int channel, RemoteEndpoint endpoint)
        {
            if(channel == 0)
            {
                return;
            }

            if (!Node.users.hasUser(endpoint.presence.wallet) || Node.users.getUser(endpoint.presence.wallet).nickData == null)
            {
                requestNickname(endpoint.presence.wallet);
                requestAvatar(endpoint.presence.wallet);
            }

            if(message.id == null)
            {
                return;
            }

            if (Messages.getMessage(message.id, channel) != null)
            {
                return;
            }
            IxiNumber price = getMessagePrice(message.sender, message.data.Length);
            if (price > 0)
            {
                // TODO TODO resend payment request after a while
                // TODO TODO remove pending message after a while
                StreamTransactionRequest str = new StreamTransactionRequest(message.id, price);
                if (pendingMessages.Find(x => x.id.SequenceEqual(message.id)) != null)
                {
                    sendBotAction(message.sender, SpixiBotActionCode.getPayment, str.getBytes(), channel);
                    return;
                }
                pendingMessages.Add(message);
                sendBotAction(message.sender, SpixiBotActionCode.getPayment, str.getBytes(), channel);
            }
            else
            {
                Messages.addMessage(message, channel);
                QuotaManager.addActivity(endpoint.presence.wallet, false);
                // Relay certain messages without transaction
                NetworkServer.forwardMessage(ProtocolMessageCode.s2data, raw_message, endpoint.presence.wallet);
            }
        }

        public static void onBotAction(byte[] action_data, RemoteEndpoint endpoint, int channel = 0)
        {
            SpixiBotAction sba = new SpixiBotAction(action_data);
            switch(sba.action)
            {
                case SpixiBotActionCode.getChannels:
                    sendChannels(endpoint);
                    break;

                case SpixiBotActionCode.getInfo:
                    Node.users.setPubKey(endpoint.presence.wallet, endpoint.serverPubKey);
                    sendInfo(endpoint.presence.wallet);
                    break;

                case SpixiBotActionCode.getUsers:
                    sendUsers(endpoint);
                    break;

                case SpixiBotActionCode.payment:
                    StreamTransaction stream_tx = new StreamTransaction(sba.data);

                    if (!stream_tx.transaction.toList.Keys.First().SequenceEqual(Node.walletStorage.getPrimaryAddress()))
                    {
                        Logging.warn("Received transaction txid " + stream_tx.transaction.id + " from " + Base58Check.Base58CheckEncoding.EncodePlain(endpoint.presence.wallet) + " that's not for this node.");
                        return;
                    }

                    StreamMessage sm = pendingMessages.Find(x => x.id.SequenceEqual(stream_tx.messageID));
                    if(sm == null)
                    {
                        // TODO TODO TODO send get message request to the client
                        Logging.warn("Received transaction txid " + stream_tx.transaction.id + " from " + Base58Check.Base58CheckEncoding.EncodePlain(endpoint.presence.wallet) + " but have no message for this transaction.");
                        return;
                    }

                    IxiNumber price = getMessagePrice(sm.sender, sm.data.Length);
                    if (stream_tx.transaction.amount < price)
                    {
                        Logging.warn("Received transaction txid " + stream_tx.transaction.id + " from " + Base58Check.Base58CheckEncoding.EncodePlain(endpoint.presence.wallet) + " that has lower than expected amount.");
                        return;
                    }

                    CoreProtocolMessage.broadcastProtocolMessage(new char[] { 'M', 'H' }, ProtocolMessageCode.newTransaction, stream_tx.transaction.getBytes(), null);
                    CoreProtocolMessage.broadcastGetTransaction(stream_tx.transaction.id, 0, null, false);
                    PendingTransactions.addPendingLocalTransaction(stream_tx.transaction, stream_tx.messageID);
                    break;

                case SpixiBotActionCode.enableNotifications:
                    bool send_notifications = false;
                    if (sba.data[0] == 1)
                    {
                        send_notifications = true;
                    }
                    Node.users.getUser(endpoint.presence.wallet).sendNotification = send_notifications;
                    Node.users.writeContactsToFile();
                    break;
            }
        }

        public static void confirmMessage(byte[] msg_id)
        {
            // TODO TODO send confirmation to client
            StreamMessage sm = pendingMessages.Find(x => x.id.SequenceEqual(msg_id));
            if (sm != null)
            {
                pendingMessages.Remove(sm);
                Messages.addMessage(sm, new SpixiMessage(sm.data).channel);
                NetworkServer.forwardMessage(ProtocolMessageCode.s2data, sm.getBytes());
            }
        }

        public static void sendBotAction(byte[] recipient, SpixiBotActionCode action, byte[] data, int channel = 0)
        {
            SpixiBotAction sba = new SpixiBotAction(action, data);

            // Prepare the message and send to the S2 nodes
            SpixiMessage spixi_message = new SpixiMessage(SpixiMessageCode.botAction, sba.getBytes(), channel);

            StreamMessage message = new StreamMessage();
            message.type = StreamMessageCode.info;
            message.recipient = recipient;
            message.sender = IxianHandler.getWalletStorage().getPrimaryAddress();
            message.transaction = new byte[1];
            message.sigdata = new byte[1];
            message.data = spixi_message.getBytes();
            message.encryptionType = StreamMessageEncryptionCode.none;

            sendMessage(recipient, message);
        }

        public static void sendChannel(byte[] recipient, BotChannel bc)
        {
            sendBotAction(recipient, SpixiBotActionCode.channel, bc.getBytes());
        }

        public static void sendChannels(RemoteEndpoint endpoint)
        {
            Dictionary<string, BotChannel> tmp_channels;
            lock (Node.channels.channels)
            {
                tmp_channels = new Dictionary<string, BotChannel>(Node.channels.channels);
            }
            foreach (var item in tmp_channels)
            {
                sendChannel(endpoint.presence.wallet, item.Value);
            }
        }

        public static void sendUser(byte[] recipient, BotContact bc)
        {
            sendBotAction(recipient, SpixiBotActionCode.user, bc.getBytes(false));
        }

        public static void sendUsers(RemoteEndpoint endpoint)
        {
            Dictionary<byte[], BotContact> tmp_contacts;
            lock (Node.users.contacts)
            {
                tmp_contacts = new Dictionary<byte[], BotContact>(Node.users.contacts);
            }
            foreach (var item in tmp_contacts)
            {
                if(item.Value.status != BotContactStatus.normal)
                {
                    continue;
                }

                sendUser(endpoint.presence.wallet, item.Value);
            }
        }

        public static void sendInfo(byte[] wallet_address)
        {
            int default_group = Int32.Parse(Node.settings.getOption("defaultGroup", "0"));

            var user = Node.users.getUser(wallet_address);
            bool send_notifications = user.sendNotification;
            int role_index = user.getPrimaryRole();
            BotGroup group;
            if (Node.groups.groupIndexToName(role_index) != "")
            {
                group = Node.groups.getGroup(Node.groups.groupIndexToName(role_index));
            }
            else
            {
                group = Node.groups.getGroup(Node.groups.groupIndexToName(default_group));
            }
            IxiNumber cost = group.messageCost;
            bool admin = false;
            if (group.admin)
            {
                admin = true;
            }
            BotInfo bi = new BotInfo(0, Node.settings.getOption("serverName", "Bot"), Node.settings.getOption("serverDescription", "Bot"), cost, Int32.Parse(Node.settings.getOption("generatedTime", "0")), admin, default_group, Int32.Parse(Node.settings.getOption("defaultChannel", "0")), send_notifications);
            sendBotAction(wallet_address, SpixiBotActionCode.info, bi.getBytes());
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

        public static void sendAcceptAdd(byte[] recipient, byte[] pub_key)
        {
            Node.users.setPubKey(recipient, pub_key);
            var user = Node.users.getUser(recipient);
            if (user != null && user.status != BotContactStatus.banned)
            {
                user.status = BotContactStatus.normal;
            }

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
            if (contact_address != null && contact_address.Length > 1)
            {
                if (!Node.users.hasUser(contact_address))
                {
                    return;
                }

                sendMessage(recipient, new StreamMessage(Node.users.getUser(contact_address).nickData));

                return;
            }

            SpixiMessage reply_spixi_message = new SpixiMessage(SpixiMessageCode.nick, Encoding.UTF8.GetBytes(Config.botName));
            byte[] sender = IxianHandler.getWalletStorage().getPrimaryAddress();

            // Send the nickname message to friend
            StreamMessage reply_message = new StreamMessage();
            reply_message.type = StreamMessageCode.info;
            reply_message.recipient = recipient;
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
            if (contact_address != null && contact_address.Length > 1)
            {
                if (!Node.users.hasUser(contact_address))
                {
                    return;
                }

                string path = Node.users.getAvatarPath(contact_address);
                if(path == null)
                {
                    return;
                }

                byte[] avatar_data = File.ReadAllBytes(path);
                if (avatar_data != null)
                {
                    sendMessage(recipient, new StreamMessage(avatar_data));
                }

                return;
            }

            byte[] avatar_bytes = Node.getAvatarBytes();

            if (avatar_bytes == null)
            {
                return;
            }

            SpixiMessage reply_spixi_message = new SpixiMessage(SpixiMessageCode.avatar, avatar_bytes);
            byte[] sender = IxianHandler.getWalletStorage().getPrimaryAddress();

            // Send the nickname message to friend
            StreamMessage reply_message = new StreamMessage();
            reply_message.type = StreamMessageCode.info;
            reply_message.recipient = recipient;
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

        // Requests the avatar of the sender
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
    }
}
