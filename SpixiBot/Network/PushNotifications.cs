using IXICore.Meta;
using SpixiBot.Meta;
using System;
using System.Linq;
using System.Net;
using System.Threading;

namespace SpixiBot.Network
{
    class PushNotifications
    {
        string serverUrl = null;

        Thread pushNotificationThread = null;
        bool running = false;

        public bool sendPushNotification = false;

        public PushNotifications(string server_url)
        {
            serverUrl = server_url;
        }

        public void start()
        {
            if(running)
            {
                return;
            }

            running = true;
            pushNotificationThread = new Thread(pushNotificationLoop);
            pushNotificationThread.Start();
        }

        public void stop()
        {
            if (!running)
            {
                return;
            }
            running = false;
            if(pushNotificationThread != null)
            {
                pushNotificationThread.Abort();
                pushNotificationThread = null;
            }
        }

        public void pushNotificationLoop()
        {
            Thread.CurrentThread.IsBackground = true;
            string sender = "";
            while(running)
            {
                if (sendPushNotification)
                {
                    sendPushNotification = false;
                    foreach (var user in Node.users.contacts)
                    {
                        if(!user.Value.sendNotification)
                        {
                            continue;
                        }
                        if (IXICore.Network.NetworkServer.connectedClients.Find(x => x.presence.wallet.SequenceEqual(user.Key)) == null)
                        {
                            sendPushMessage(Base58Check.Base58CheckEncoding.EncodePlain(user.Key), sender, true);
                        }
                    }
                }

                Thread.Sleep(60000);
            }
        }

        private bool sendPushMessage(string receiver, string sender, bool push)
        {
            string data = "";

            string URI = String.Format("{0}/push.php", serverUrl);
            string parameters = String.Format("tag={0}&data={1}&pk={2}&push={3}&fa={4}", receiver, data, "", push, sender);

            using (WebClient client = new WebClient())
            {
                try
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                    string htmlCode = client.UploadString(URI, parameters);
                    if (htmlCode.Equals("OK"))
                    {
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Logging.error("Exception occured in sendPushMessage: " + e);
                }
            }
            return false;
        }
    }
}
