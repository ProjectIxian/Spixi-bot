using IXICore;
using IXICore.SpixiBot;
using SpixiBot.Meta;
using SpixiBot.Network;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SpixiBot
{
    class APIServer : GenericAPIServer
    {
        public APIServer(List<string> listen_URLs, Dictionary<string, string> authorized_users = null, List<string> allowed_IPs = null)
        {
            // Start the API server
            start(listen_URLs, authorized_users, allowed_IPs);
        }

        protected override bool processRequest(HttpListenerContext context, string methodName, Dictionary<string, object> parameters)
        {
            JsonResponse response = null;

            if (methodName.Equals("sb_settings", StringComparison.OrdinalIgnoreCase))
            {
                response = onSettings(parameters);
            }
            
            if (methodName.Equals("sb_setOption", StringComparison.OrdinalIgnoreCase))
            {
                response = onSetOption(parameters);
            }

            if (methodName.Equals("sb_getGroups", StringComparison.OrdinalIgnoreCase))
            {
                response = onGetGroups(parameters);
            }

            if (methodName.Equals("sb_newGroup", StringComparison.OrdinalIgnoreCase))
            {
                response = onNewGroup(parameters);
            }

            if (methodName.Equals("sb_delGroup", StringComparison.OrdinalIgnoreCase))
            {
                response = onDelGroup(parameters);
            }

            if (methodName.Equals("sb_getUsers", StringComparison.OrdinalIgnoreCase))
            {
                response = onGetUsers(parameters);
            }

            if (methodName.Equals("sb_delUser", StringComparison.OrdinalIgnoreCase))
            {
                response = onDelUser(parameters);
            }

            if (methodName.Equals("sb_setUserGroup", StringComparison.OrdinalIgnoreCase))
            {
                response = onSetUserGroup(parameters);
            }

            if (methodName.Equals("sb_getChannels", StringComparison.OrdinalIgnoreCase))
            {
                response = onGetChannels(parameters);
            }

            if (methodName.Equals("sb_newChannel", StringComparison.OrdinalIgnoreCase))
            {
                response = onNewChannel(parameters);
            }

            if (methodName.Equals("sb_delChannel", StringComparison.OrdinalIgnoreCase))
            {
                response = onDelChannel(parameters);
            }


            if (response == null)
            {
                return false;
            }

            // Set the content type to plain to prevent xml parsing errors in various browsers
            context.Response.ContentType = "application/json";

            sendResponse(context.Response, response);

            context.Response.Close();

            return true;
        }

        public JsonResponse onSettings(Dictionary<string, object> parameters)
        {
            JsonError error = null;

            Dictionary<string, object> status_array = new Dictionary<string, object>();

            status_array.Add("serverName", Node.settings.getOption("serverName", Config.botName));
            status_array.Add("serverDescription", Node.settings.getOption("serverDescription", ""));
            status_array.Add("serverPassword", Node.settings.getOption("serverPassword", ""));
            status_array.Add("allowFileTransfer", Node.settings.getOption("allowFileTransfer", "0"));
            status_array.Add("fileTransferLimitMB", Node.settings.getOption("fileTransferLimitMB", "10"));
            status_array.Add("defaultGroup", Node.groups.groupIndexToName(Int32.Parse(Node.settings.getOption("defaultGroup", "0"))));
            status_array.Add("defaultChannel", Node.channels.channelIndexToName(Int32.Parse(Node.settings.getOption("defaultChannel", "0"))));


            return new JsonResponse { result = status_array, error = error };
        }

        public JsonResponse onSetOption(Dictionary<string, object> parameters)
        {
            JsonError error = null;

            if (parameters.ContainsKey("serverName"))
            {
                Config.botName = (string)parameters["serverName"];
                Node.settings.setOption("serverName", (string)parameters["serverName"]);
            }

            if (parameters.ContainsKey("serverDescription"))
            {
                Node.settings.setOption("serverDescription", (string)parameters["serverDescription"]);
            }

            if (parameters.ContainsKey("serverPassword"))
            {
                Node.settings.setOption("serverPassword", (string)parameters["serverPassword"]);
            }

            if (parameters.ContainsKey("allowFileTransfer"))
            {
                Node.settings.setOption("allowFileTransfer", (string)parameters["allowFileTransfer"]);
            }

            if (parameters.ContainsKey("fileTransferLimitMB"))
            {
                Node.settings.setOption("fileTransferLimitMB", (string)parameters["fileTransferLimitMB"]);
            }

            if (parameters.ContainsKey("defaultGroup"))
            {
                Node.settings.setOption("defaultGroup", Node.groups.getGroup((string)parameters["defaultGroup"]).index.ToString());
            }

            if (parameters.ContainsKey("defaultChannel"))
            {
                Node.settings.setOption("defaultChannel", Node.channels.getChannel((string)parameters["defaultChannel"]).index.ToString());
            }

            return new JsonResponse { result = "", error = error };
        }

        public JsonResponse onGetUsers(Dictionary<string, object> parameters)
        {
            JsonError error = null;

            Dictionary<string, object> contacts_array = new Dictionary<string, object>();

            lock (Node.users.contacts)
            {
                foreach (var contact in Node.users.contacts)
                {
                    Dictionary<string, object> contact_array = new Dictionary<string, object>();
                    contact_array.Add("nick", contact.Value.getNick());
                    int role = contact.Value.getPrimaryRole();
                    contact_array.Add("role", Node.groups.groupIndexToName(role));
                    contacts_array.Add(Base58Check.Base58CheckEncoding.EncodePlain(contact.Key), contact_array);
                }
            }

            return new JsonResponse { result = contacts_array, error = error };
        }

        public JsonResponse onDelUser(Dictionary<string, object> parameters)
        {
            JsonError error = null;

            Node.users.delUser(Base58Check.Base58CheckEncoding.DecodePlain((string)parameters["address"]));

            return new JsonResponse { result = "", error = error };
        }

        public JsonResponse onSetUserGroup(Dictionary<string, object> parameters)
        {
            JsonError error = null;
            string role = (string)parameters["role"];
            var group = Node.groups.getGroup(role);
            int role_id = 0;
            if(group != null)
            {
                role_id = group.index;
            }
            byte[] address = Base58Check.Base58CheckEncoding.DecodePlain((string)parameters["address"]);
            Node.users.setRole(address, role_id);
            StreamProcessor.sendInfo(address);

            return new JsonResponse { result = "", error = error };
        }

        public JsonResponse onGetChannels(Dictionary<string, object> parameters)
        {
            JsonError error = null;

            Dictionary<string, object> channels_array = new Dictionary<string, object>();

            lock (Node.channels.channels)
            {
                foreach (var channel in Node.channels.channels)
                {
                    channels_array.Add(channel.Key, channel.Value.channelName);
                }
            }

            return new JsonResponse { result = channels_array, error = error };
        }

        public JsonResponse onNewChannel(Dictionary<string, object> parameters)
        {
            JsonError error = null;

            if (Node.channels.hasChannel((string)parameters["channel"]))
            {
                return new JsonResponse { result = null, error = new JsonError() { code = (int)RPCErrorCode.RPC_INVALID_PARAMETER, message = "Channel already exists." } };
            }

            BotChannel channel = new BotChannel(Node.channels.getNextIndex(), (string)parameters["channel"]);
            Node.channels.setChannel((string)parameters["channel"], channel);
            Node.settings.saveSettings();

            Messages.addChannel(channel);

            return new JsonResponse { result = "", error = error };
        }

        public JsonResponse onUpdateChannel(Dictionary<string, object> parameters)
        {
            JsonError error = null;

            if (!Node.channels.hasChannel((string)parameters["channel"]))
            {
                return new JsonResponse { result = null, error = new JsonError() { code = (int)RPCErrorCode.RPC_INVALID_PARAMETER, message = "Unknown channel." } };
            }

            var orig_channel = Node.channels.getChannel((string)parameters["channel"]);

            Node.channels.setChannel((string)parameters["channel"], new BotChannel(orig_channel.index, (string)parameters["value"]));
            Node.settings.saveSettings();

            return new JsonResponse { result = "", error = error };
        }

        public JsonResponse onDelChannel(Dictionary<string, object> parameters)
        {
            JsonError error = null;

            Node.channels.setChannel((string)parameters["channel"], null);
            Node.settings.saveSettings();

            return new JsonResponse { result = "", error = error };
        }

        public JsonResponse onGetGroups(Dictionary<string, object> parameters)
        {
            JsonError error = null;

            Dictionary<string, object> groups_array = new Dictionary<string, object>();

            lock (Node.groups.groups)
            {
                foreach (var group in Node.groups.groups)
                {
                    Dictionary<string, object> group_array = new Dictionary<string, object>();
                    group_array.Add("cost", group.Value.messageCost.ToString());
                    group_array.Add("admin", group.Value.admin.ToString());

                    groups_array.Add(group.Value.groupName, group_array);
                }
            }

            return new JsonResponse { result = groups_array, error = error };
        }

        public JsonResponse onDelGroup(Dictionary<string, object> parameters)
        {
            JsonError error = null;

            Node.groups.setGroup((string)parameters["group"], null);
            Node.settings.saveSettings();

            return new JsonResponse { result = null, error = error };
        }

        public JsonResponse onNewGroup(Dictionary<string, object> parameters)
        {
            JsonError error = null;

            string group = (string)parameters["group"];
            IxiNumber cost = new IxiNumber((string)parameters["cost"]);
            bool admin = false;
            if((string)parameters["admin"] == "1")
            {
                admin = true;
            }

            if (Node.groups.hasGroup(group))
            {
                return new JsonResponse { result = "", error = new JsonError() { code = (int)RPCErrorCode.RPC_INVALID_PARAMETER, message = "Group already exists." } };
            }

            Node.groups.setGroup(group, new BotGroup(Node.groups.getNextIndex(), group, cost, admin));
            Node.settings.saveSettings();

            return new JsonResponse { result = "", error = error };
        }

        public JsonResponse onUpdateGroup(Dictionary<string, object> parameters)
        {
            JsonError error = null;

            string old_group = (string)parameters["oldGroup"];
            string group = (string)parameters["group"];
            IxiNumber cost = new IxiNumber((string)parameters["cost"]);
            bool admin = false;
            if ((string)parameters["admin"] == "1")
            {
                admin = true;
            }

            if (Node.groups.hasGroup(old_group))
            {
                return new JsonResponse { result = null, error = new JsonError() { code = (int)RPCErrorCode.RPC_INVALID_PARAMETER, message = "Unknown group." } };
            }

            Node.groups.setGroup(group, new BotGroup(Node.groups.getNextIndex(), group, cost, admin));
            Node.settings.saveSettings();

            return new JsonResponse { result = "", error = error };
        }
    }
}
