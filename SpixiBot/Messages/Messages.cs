using IXICore;
using IXICore.Meta;
using IXICore.SpixiBot;
using SpixiBot.Meta;
using SpixiBot.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpixiBot
{
    static class Messages
    {
        static Dictionary<int, List<StreamMessage>> messages = new Dictionary<int, List<StreamMessage>>(); // List that stores stream messages
        static string messagesBasePath = "";


        public static void init(string base_path)
        {
            if (!Directory.Exists(base_path))
            {
                Directory.CreateDirectory(base_path);
            }
            messagesBasePath = base_path;
            loadMessagesFromFiles();
        }


        private static void loadMessagesFromFiles()
        {
            foreach (var channel in Node.channels.channels)
            {
                int channel_index = channel.Value.index;
                messages.Add(channel_index, new List<StreamMessage>());
                loadMessagesFromFile(Path.Combine(messagesBasePath, channel_index.ToString(), "messages.ixi"), channel_index);
            }
        }

        private static void loadMessagesFromFile(string messagesPath, int channel)
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
                    Logging.error("Cannot open {0} file: {1}", messagesPath, e.Message);
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

                        messages[channel].Add(new StreamMessage(msg_bytes));
                    }
                }
                catch (Exception e)
                {
                    Logging.error("Cannot read from {0} file: {0}", messagesPath, e.Message);
                    // TODO TODO notify the user or something like that
                }

                reader.Close();
            }
        }

        private static void writeMessagesToFile(int channel)
        {
            string channel_base_path = Path.Combine(messagesBasePath, channel.ToString());
            if (!Directory.Exists(channel_base_path))
            {
                Directory.CreateDirectory(channel_base_path);
            }
            string messagesPath = Path.Combine(channel_base_path, "messages.ixi");
            lock (messages)
            {
                FileStream fs;
                BinaryWriter writer;
                try
                {
                    // Prepare the file for writing
                    fs = new FileStream(messagesPath, FileMode.Create);
                    writer = new BinaryWriter(fs);
                }
                catch (IOException e)
                {
                    Logging.error("Cannot create {0} file: {0}", messagesPath, e.Message);
                    return;
                }

                try
                {
                    int version = 0;
                    writer.Write(version);

                    int message_num = messages[channel].Count;
                    writer.Write(message_num);

                    foreach (var message in messages[channel])
                    {
                        byte[] msg_bytes = message.getBytes();
                        writer.Write(msg_bytes.Length);
                        writer.Write(msg_bytes);
                    }
                }
                catch (IOException e)
                {
                    Logging.error("Cannot write to {0} file: {0}", messagesPath, e.Message);
                }
                writer.Flush();
                writer.Close();
                writer.Dispose();

                fs.Close();
                fs.Dispose();
            }
        }

        public static void addMessage(StreamMessage msg, int channel)
        {
            if(msg.id == null)
            {
                return;
            }
            lock(messages)
            {
                var old_msg = messages[channel].Find(x => x.id.SequenceEqual(msg.id));
                if (old_msg == null)
                {
                    messages[channel].Add(msg);
                    if (messages[channel].Count > Config.maxMessagesPerChannel)
                    {
                        messages[channel].RemoveAt(0);
                    }
                    writeMessagesToFile(channel);
                    Node.pushNotifications.sendPushNotification = true;
                }
            }
        }

        public static StreamMessage getMessage(byte[] id, int channel)
        {
            lock(messages)
            {
                if(!messages.ContainsKey(channel))
                {
                    Logging.error("Error getting message from channel {0}, channel doesn't exist.", channel);
                }
                return messages[channel].Find(x => x.id.SequenceEqual(id));
            }
        }

        public static void removeMessage(byte[] id, int channel)
        {
            lock (messages)
            {
                var msg = messages[channel].Find(x => x.id.SequenceEqual(id));
                if(msg != null)
                {
                    msg.data = null;
                    writeMessagesToFile(channel);
                }
            }
        }

        public static void addChannel(BotChannel channel)
        {
            lock (messages)
            {
                if (!messages.ContainsKey(channel.index))
                {
                    messages.Add(channel.index, new List<StreamMessage>());
                }
            }
        }

        public static void sendMessages(byte[] recipient_address, int channel, byte[] last_message_id)
        {
            lock (messages)
            {
                int last_msg_index = -1;
                if (last_message_id != null)
                {
                    last_msg_index = messages[channel].FindLastIndex(x => x.id.SequenceEqual(last_message_id));
                }
                for (int i = last_msg_index + 1; i < messages[channel].Count; i++)
                {
                    if(messages[channel][i].data == null)
                    {
                        // deleted
                        continue;
                    }
                    StreamProcessor.sendMessage(recipient_address, messages[channel][i]);
                }
            }
        }
    }
}
