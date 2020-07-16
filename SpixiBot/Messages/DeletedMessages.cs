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
    static class DeletedMessages
    {
        static Dictionary<int, List<byte[]>> messages = new Dictionary<int, List<byte[]>>(); // List that stores stream messages
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
                messages.Add(channel_index, new List<byte[]>());
                loadMessagesFromFile(Path.Combine(messagesBasePath, channel_index.ToString(), "delmsgs.ixi"), channel_index);
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
                        byte[] msg_dd_bytes = reader.ReadBytes(msg_len);

                        messages[channel].Add(msg_dd_bytes);
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
            string messagesPath = Path.Combine(channel_base_path, "delmsgs.ixi");
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
                    Logging.error("Cannot create {0} file: {0}", messagesPath, e.Message);
                    return;
                }

                try
                {
                    int version = 0;
                    writer.Write(version);

                    int message_num = messages.Count;
                    writer.Write(message_num);

                    foreach (var msg_id in messages[channel])
                    {
                        writer.Write(msg_id.Length);
                        writer.Write(msg_id);
                    }
                }
                catch (IOException e)
                {
                    Logging.error("Cannot write to {0} file: {0}", messagesPath, e.Message);
                }
                writer.Close();
            }
        }

        public static void addMessage(byte[] msg_id, int channel)
        {
            lock (messages)
            {
                var old_msg = messages[channel].Find(x => x.SequenceEqual(msg_id));
                if (old_msg == null)
                {
                    messages[channel].Add(msg_id);
                    if (messages.Count > Config.maxMessagesPerChannel)
                    {
                        messages[channel].RemoveAt(0);
                    }
                    writeMessagesToFile(channel);
                }
            }
        }

        public static bool hasMessageId(byte[] id, int channel)
        {
            lock (messages)
            {
                if(messages[channel].Find(x => x.SequenceEqual(id)) != null)
                {
                    return true;
                }
            }
            return false;
        }

        public static void removeMessage(byte[] id, int channel)
        {
            lock (messages)
            {
                var msg = messages[channel].Find(x => x.SequenceEqual(id));
                if (msg != null)
                {
                    messages[channel].Remove(msg);
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
                    messages.Add(channel.index, new List<byte[]>());
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
                    last_msg_index = messages[channel].FindLastIndex(x => x.SequenceEqual(last_message_id));
                }
                for (int i = last_msg_index + 1; i < messages[channel].Count; i++)
                {
                    StreamProcessor.sendMsgDelete(recipient_address, messages[channel][i], channel);
                }
            }
        }
    }
}
