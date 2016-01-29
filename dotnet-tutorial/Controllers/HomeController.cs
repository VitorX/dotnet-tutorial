// Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license. See full license at the bottom of this file.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

using System.Threading.Tasks;
using Microsoft.Experimental.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Office365.OutlookServices;

namespace dotnet_tutorial.Controllers
{
    public class HomeController : Controller
    {
        // The required scopes for our app
        private static string[] scopes = { 
                                           "https://outlook.office.com/mail.read",
                                           "https://outlook.office.com/calendars.readwrite",
                                           "https://outlook.office.com/contacts.read"
                                         };

        public ActionResult Index()
        {
            return View();
        }

        public async Task<ActionResult> SignIn()
        {
            string authority = "https://login.microsoftonline.com/common";
            string clientId = System.Configuration.ConfigurationManager.AppSettings["ida:ClientID"]; 
            AuthenticationContext authContext = new AuthenticationContext(authority);

            // The url in our app that Azure should redirect to after successful signin
            Uri redirectUri = new Uri(Url.Action("Authorize", "Home", null, Request.Url.Scheme));

            // Generate the parameterized URL for Azure signin
            Uri authUri = await authContext.GetAuthorizationRequestUrlAsync(scopes, null, clientId, redirectUri, UserIdentifier.AnyUser, null);

            // Redirect the browser to the Azure signin page
            return Redirect(authUri.ToString());
        }

        // Note the function signature is changed!
        public async Task<ActionResult> Authorize()
        {
            // Get the 'code' parameter from the Azure redirect
            string authCode = Request.Params["code"];

            string authority = "https://login.microsoftonline.com/common";
            string clientId = System.Configuration.ConfigurationManager.AppSettings["ida:ClientID"]; ;
            string clientSecret = System.Configuration.ConfigurationManager.AppSettings["ida:ClientSecret"]; ;
            AuthenticationContext authContext = new AuthenticationContext(authority);
            
            // The same url we specified in the auth code request
            Uri redirectUri = new Uri(Url.Action("Authorize", "Home", null, Request.Url.Scheme));

            // Use client ID and secret to establish app identity
            ClientCredential credential = new ClientCredential(clientId, clientSecret);

            try
            {
                // Get the token
                var authResult = await authContext.AcquireTokenByAuthorizationCodeAsync(
                    authCode, redirectUri, credential, scopes);

                // Save the token in the session
                Session["access_token"] = authResult.Token;
                
                // Try to get user info
                Session["user_email"] = GetUserEmail(authContext, clientId);

                return Redirect(Url.Action("Inbox", "Home", null, Request.Url.Scheme));
            }
            catch (AdalException ex)
            {
                return Content(string.Format("ERROR retrieving token: {0}", ex.Message));
            }
        }

        public async Task<ActionResult> Inbox()
        {
            string token = (string)Session["access_token"];
            string email = (string)Session["user_email"];
            if (string.IsNullOrEmpty(token))
            {
                // If there's no token in the session, redirect to Home
                return Redirect("/");
            }

            try
            {
                OutlookServicesClient client = new OutlookServicesClient(new Uri("https://outlook.office.com/api/v2.0"),
                    async () =>
                    {
                        // Since we have it locally from the Session, just return it here.
                        return token;
                    });

                client.Context.SendingRequest2 += new EventHandler<Microsoft.OData.Client.SendingRequest2EventArgs> (
                    (sender, e) => InsertXAnchorMailboxHeader(sender, e, email));

                var mailResults = await client.Me.Messages
                                  .OrderByDescending(m => m.ReceivedDateTime)
                                  .Take(10)
                                  .Select(m => new Models.DisplayMessage(m.Subject, m.ReceivedDateTime, m.From))
                                  .ExecuteAsync();

                return View(mailResults.CurrentPage);
            }
            catch (AdalException ex)
            {
                return Content(string.Format("ERROR retrieving messages: {0}", ex.Message));
            }
        }

        public async Task<ActionResult> Calendar()
        {
            string token = (string)Session["access_token"];
            string email = (string)Session["user_email"];
            if (string.IsNullOrEmpty(token))
            {
                // If there's no token in the session, redirect to Home
                return Redirect("/");
            }

            try
            {
                OutlookServicesClient client = new OutlookServicesClient(new Uri("https://outlook.office.com/api/v2.0"),
                    async () =>
                    {
                        // Since we have it locally from the Session, just return it here.
                        return token;
                    });

                client.Context.SendingRequest2 += new EventHandler<Microsoft.OData.Client.SendingRequest2EventArgs>(
                    (sender, e) => InsertXAnchorMailboxHeader(sender, e, email));

                var eventResults = await client.Me.Events
                                    .OrderByDescending(e => e.Start.DateTime)
                                    .Take(10)
                                    .Select(e => new Models.DisplayEvent(e.Subject, e.Start.DateTime, e.End.DateTime))
                                    .ExecuteAsync();

                return View(eventResults.CurrentPage);
            }
            catch (AdalException ex)
            {
                return Content(string.Format("ERROR retrieving events: {0}", ex.Message));
            }
        }

        public async Task<ActionResult> Contacts()
        {
            string token = (string)Session["access_token"];
            string email = (string)Session["user_email"];
            if (string.IsNullOrEmpty(token))
            {
                // If there's no token in the session, redirect to Home
                return Redirect("/");
            }

            try
            {
                OutlookServicesClient client = new OutlookServicesClient(new Uri("https://outlook.office.com/api/v2.0"),
                    async () =>
                    {
                        // Since we have it locally from the Session, just return it here.
                        return token;
                    });

                client.Context.SendingRequest2 += new EventHandler<Microsoft.OData.Client.SendingRequest2EventArgs>(
                    (sender, e) => InsertXAnchorMailboxHeader(sender, e, email));

                var contactResults = await client.Me.Contacts
                                    .OrderBy(c => c.DisplayName)
                                    .Take(10)
                                    .Select(c => new Models.DisplayContact(c.DisplayName, c.EmailAddresses, c.MobilePhone1))
                                    .ExecuteAsync();

                return View(contactResults.CurrentPage);
            }
            catch (AdalException ex)
            {
                return Content(string.Format("ERROR retrieving contacts: {0}", ex.Message));
            }
        }

        private void InsertXAnchorMailboxHeader(object sender, Microsoft.OData.Client.SendingRequest2EventArgs e, string email)
        {
            e.RequestMessage.SetHeader("X-AnchorMailbox", email);
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        
        public async Task<ActionResult> Create()
        {
            string token = (string)Session["access_token"];
            string email = (string)Session["user_email"];
            if (string.IsNullOrEmpty(token))
            {
                // If there's no token in the session, redirect to Home
                return Redirect("/");
            }

            try
            {
                OutlookServicesClient client = new OutlookServicesClient(new Uri("https://outlook.office.com/api/v2.0"),
                    async () =>
                    {
                        // Since we have it locally from the Session, just return it here.
                        return token;
                    });

              
                Location location = new Location
                {
                    DisplayName = "Water cooler"
                };

                // Create a description for the event    
                ItemBody body = new ItemBody
                {
                    Content = "Status updates, blocking issues, and next steps",
                    ContentType = BodyType.Text
                };

                // Create the event object
                DateTimeTimeZone start=new DateTimeTimeZone() ;
                string dateTimeFormat = "yyyy-MM-ddThh:mm:ss";
                string timeZone = "Pacific Standard Time";//"Eastern Standard Time";

                start.DateTime = new DateTime(2016, 1, 22, 14, 30, 0).ToString(dateTimeFormat);
                start.TimeZone = timeZone;

                DateTimeTimeZone end = new DateTimeTimeZone();
                end.DateTime = new DateTime(2016, 1, 22, 15, 30, 0).ToString(dateTimeFormat);
                end.TimeZone = timeZone;

                Event newEvent = new Event
                {
                    Subject = "Sync up",
                    Location = location,
                    Start = start,
                    End = end,
                    Body = body
                };

                newEvent.Recurrence = new PatternedRecurrence();
                newEvent.Recurrence.Range = new RecurrenceRange();

                string dateFormat = "yyyy-MM-dd";
                newEvent.Recurrence.Range.EndDate = DateTime.Now.AddYears(1).ToString(dateFormat);
                newEvent.Recurrence.Range.StartDate = DateTime.Now.ToString(dateFormat);
                newEvent.Recurrence.Range.NumberOfOccurrences = 11;

                newEvent.Recurrence.Pattern = new RecurrencePattern();
                newEvent.Recurrence.Pattern.Type = RecurrencePatternType.Weekly;
                newEvent.Recurrence.Pattern.Interval = 1;
                newEvent.Recurrence.Pattern.DaysOfWeek= new List<Microsoft.Office365.OutlookServices.DayOfWeek>() { Microsoft.Office365.OutlookServices.DayOfWeek.Friday };
                // Add the event to the default calendar
                await client.Me.Events.AddEventAsync(newEvent);


                //client.Me.Calendars.AddCalendarAsync()
                //client.Me.Calendars.AddCalendarAsync(new ICalendar)
                var eventResults = await client.Me.Events
                                    .OrderByDescending(e => e.Start.DateTime)
                                    .Take(10)
                                    .Select(e => new Models.DisplayEvent(e.Subject, e.Start.DateTime, e.End.DateTime))
                                    .ExecuteAsync();

                return View("Calendar",eventResults.CurrentPage);
            }
            catch (AdalException ex)
            {
                return Content(string.Format("ERROR retrieving events: {0}", ex.Message));
            }

        }

        private string GetUserEmail(AuthenticationContext context, string clientId)
        {
            // ADAL caches the ID token in its token cache by the client ID
            foreach (TokenCacheItem item in context.TokenCache.ReadItems())
            {
                if (item.Scope.Contains(clientId))
                {
                    return GetEmailFromIdToken(item.Token);
                }
            }

            return string.Empty;
        }

        private string GetEmailFromIdToken(string token)
        {
            // JWT is made of three parts, separated by a '.' 
            // First part is the header 
            // Second part is the token 
            // Third part is the signature 
            string[] tokenParts = token.Split('.');

            if (tokenParts.Length < 3)
            {
                // Invalid token, return empty
            }

            // Token content is in the second part, in urlsafe base64
            string encodedToken = tokenParts[1];

            // Convert from urlsafe and add padding if needed
            int leftovers = encodedToken.Length % 4;
            if (leftovers == 2)
            {
                encodedToken += "==";
            }
            else if (leftovers == 3)
            {
                encodedToken += "=";
            }
            encodedToken = encodedToken.Replace('-', '+').Replace('_', '/');

            // Decode the string
            var base64EncodedBytes = System.Convert.FromBase64String(encodedToken);
            string decodedToken = System.Text.Encoding.UTF8.GetString(base64EncodedBytes);

            // Load the decoded JSON into a dynamic object
            dynamic jwt = Newtonsoft.Json.JsonConvert.DeserializeObject(decodedToken);

            // User's email is in the preferred_username field
            return jwt.preferred_username;
        }

        public ContentResult GetJSON()
        {
            string JSONData = "{\"name\":\"jack\"}";
            return this.Content(JSONData, "application/json");

        }

        //public JsonResult GetJSON()
        //{
        //    return Json(new Person() { name = "Jack1" },JsonRequestBehavior.AllowGet);
        //}

        public class Person {
            public string name;
        }
    }
}

// MIT License:

// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// ""Software""), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:

// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.