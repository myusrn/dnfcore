﻿using System;
using System.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using MyUsrn.Dnx.Core;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Collections.Generic;

namespace Core.Tests
{
    [TestClass]
    public class UnitTest1
    {
        string clientIdNca = ConfigurationManager.AppSettings["ida:ClientIdNca"];
        string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        string appKey = ConfigurationManager.AppSettings["ida:AppKey"];
        string authority = string.Format("https://login.microsoftonline.com/{0}", ConfigurationManager.AppSettings["ida:TenantId"]);
        //string resource = "https://microsoft.onmicrosoft.com/MyAppSvc"; // aka appId for myappsvc
        string resource = "https://microsoft.onmicrosoft.com/MyWebApi"; // aka appId for mywebapi
        string aadGraphResource = "https://graph.windows.net/"; // aka appId for aad graph api
        string msftGraphResource = "https://graph.microsoft.com/"; // aka appId for msft graph api
        string testUserId = ConfigurationManager.AppSettings["ida:TestUserId"];

        [TestMethod]
        public async Task TestCacheWithAcNca()
        {
            AuthenticationContext acNca = new AuthenticationContext(authority, new FileTokenCache());
            //AuthenticationContext acNca = new AuthenticationContext(authority, new AzRedisTokenCache());
            var redirectUri = new Uri("http://mynca/");
            AuthenticationResult result = null;
            try
            {
                result = await acNca.AcquireTokenSilentAsync(resource, clientIdNca); // attempt signin that simply retrieves still valid token from cache
                //result = acClient.AcquireToken(resource, clientIdNca, redirectUri, PromptBehavior.Never);
            }
            catch (AdalException ex)
            {
                if (ex.ErrorCode == "failed_to_acquire_token_silently") // there is no token in the client cache so attempt signin using credentials
                {
                    var userCredential = new UserCredential(ConfigurationManager.AppSettings["ida:user"], ConfigurationManager.AppSettings["ida:password"]);
                    result = await acNca.AcquireTokenAsync(resource, clientIdNca, userCredential);
                    //result = acNca.AcquireToken(resource, clientIdNca, redirectUri, PromptBehavior.Always);
                }
            }

            var jwt = GetJsonWebToken(result.AccessToken);
            var oid = jwt["body"]["oid"].Value<string>();
        }

        [TestMethod]
        public async Task TestCacheWithAcWba()
        {
            AuthenticationContext acWba = new AuthenticationContext(authority, new AzRedisTokenCache(testUserId));
            //var redirectUri = new Uri("https://myappsvc.azurewebsites.net/");
            var redirectUri = new Uri("https://mywebapp.azurewebsites.net/");
            ClientCredential wbaCredential = new ClientCredential(clientId, appKey);

            //AuthenticationResult result = acWba.AcquireTokenByAuthorizationCodeAsync(code, redirectUri, credential, aadGraphResource);
            //AuthenticationResult result = acWba.AcquireTokenByRefreshTokenAsync(refreshToken, redirectUri, credential, aadGraphResource);
        }

        /// <summary>
        /// Returns object containing the three json web token parts contained in encoded access token.
        /// </summary>
        /// <param name="encodedAccessToken"></param>
        /// <returns>A json object with header, body and signature properties.</returns>
        /// <remarks>In client layer since we have no way of safely maintaining token issuers signing cert then we have no way of verifiying signature
        /// and so you should not trust jwt property values the way that server side processing can given it can do signature verification.</remarks>
        JObject GetJsonWebToken(string encodedAccessToken)
        {
            if (string.IsNullOrEmpty(encodedAccessToken)) throw new ArgumentNullException("String to extract token from cannot be null or empty.");
                    
            var jwtValues = Base64StringDecodeEx(encodedAccessToken);
            if (jwtValues.Count != 3) throw new ArgumentOutOfRangeException("String to extract token from did not contain a header, body and signature.");

            var result = new JObject();

            var jwtHeader = JObject.Parse(jwtValues[0]);
            result.Add("header", jwtHeader);

            var jwtBody = JObject.Parse(jwtValues[1]);
            result.Add("body", jwtBody);

            result.Add("signature", jwtValues[2]);

            return result;
        }

        List<string> Base64StringDecodeEx(string arg)
        {
            if (string.IsNullOrEmpty(arg)) throw new ArgumentNullException("String to decode cannot be null or empty.");

            string[] values = new string[] { arg };
            if (arg.Contains(".")) values = arg.Split('.');

            List<string> result = new List<string>();
            foreach (var value in values)
            {
                result.Add(Base64StringDecode(value));
            }

            return result;
        }

        string Base64StringDecode(string arg)
        {
            if (string.IsNullOrEmpty(arg)) throw new ArgumentNullException("String to decode cannot be null or empty.");

            return Encoding.UTF8.GetString(ConvertFromBase64StringEx(arg));
        }

        byte[] ConvertFromBase64StringEx(string arg)
        {
            if (string.IsNullOrEmpty(arg)) throw new ArgumentNullException("String to convert cannot be null or empty.");

            StringBuilder s = new StringBuilder(arg);

            const char Base64UrlCharacter62 = '-', Base64Character62 = '+';
            s.Replace(Base64UrlCharacter62, Base64Character62);

            const char Base64UrlCharacter63 = '_', Base64Character63 = '/';
            s.Replace(Base64UrlCharacter63, Base64Character63);

            const char Base64PadCharacter = '=';
            int pad = s.Length % 4;
            s.Append(Base64PadCharacter, (pad == 0) ? 0 : 4 - pad);
// if we don't do the above then Convert.FromBase64String throws "System.FormatException: Invalid length for a Base-64 char array or string."
// typically arising when processing token body part and not the header or signature parts

            return Convert.FromBase64String(s.ToString());
        }
    }
}