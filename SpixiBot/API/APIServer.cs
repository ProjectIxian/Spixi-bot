using IXICore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;

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
    }
}
