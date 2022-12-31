using IXICore.Meta;
using SpixiBot.Meta;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
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
                pushNotificationThread.Interrupt();
                pushNotificationThread.Join();
                pushNotificationThread = null;
            }
        }

        public void pushNotificationLoop()
        {
            Thread.CurrentThread.IsBackground = true;
            string sender = "";
            while(running)
            {
                try
                {
                    if (sendPushNotification)
                    {
                        sendPushNotification = false;
                        foreach (var user in Node.users.contacts)
                        {
                            try
                            {
                                if (user.Value.status != IXICore.SpixiBot.BotContactStatus.normal)
                                {
                                    continue;
                                }
                                if (!user.Value.sendNotification)
                                {
                                    continue;
                                }
                                if (IXICore.Network.NetworkServer.connectedClients.Find(x => x.presence != null && x.presence.wallet.SequenceEqual(user.Key)) == null)
                                {
                                    while (!sendPushMessage(user.Key.ToString(), sender, true))
                                    {
                                        Thread.Sleep(1000);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Logging.error("Exception occured in pushNotificationLoop: " + e);
                            }
                            Thread.Sleep(100);
                        }
                    }
                }catch(Exception e)
                {
                    Logging.error("Exception occured in pushNotificationLoop: " + e);
                }

                Thread.Sleep(Config.pushNotificationInterval);
            }
        }

        private bool sendPushMessage(string receiver, string sender, bool push)
        {
            string data = "";

            string URI = String.Format("{0}/push.php", serverUrl);
            string parameters = String.Format("tag={0}&data={1}&pk={2}&push={3}&fa={4}", receiver, data, "", push, sender);

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpContent httpContent = new StringContent(parameters, Encoding.UTF8, "application/x-www-form-urlencoded");
                    var response = client.PostAsync(URI, httpContent).Result;
                    string body = response.Content.ReadAsStringAsync().Result;
                    if (body.Equals("OK"))
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
