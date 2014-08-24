﻿using HRI.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using Umbraco.Core.Models;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace HRI.Controllers
{
    public class EmailSurfaceController : SurfaceController
    {
        /// <summary>
        /// Build an email message from a template.
        /// </summary>
        /// <param name="template">The template file located in the ~/EmailTemplates folder</param>
        /// <param name="values">A dictionary that contains the dynamic placeholder as a key, and has the text to insert as the value. (ex item["<%UserName%>", model.UserName])</param>
        /// <returns>A string representation of the email with all the dynamic text replaced by the provided values</returns>
        private string BuildEmail(int emailTemplateId, IDictionary<string, string> values)
        {
            // Create a string to hold the email text
            string emailMessage;
            // Get an instance of the template
            var mediaItem = Services.MediaService.GetById(emailTemplateId);
            // Get the path to the template
            string path = Server.MapPath(mediaItem.Properties["umbracoFile"].Value as string);
            // Open a Stream Reader to read in all the text from the template
            using (StreamReader sr = new StreamReader(path, Encoding.UTF8))
            {
                // Read all the text into the emailMessage string
                emailMessage = sr.ReadToEnd();
                // Close the stream
                sr.Close();
            }
            // For each dynamic item in the template
            foreach(KeyValuePair<string,string> dynamicTextItem in values)
            {
                // Replace the dynamic item with the values member info
                emailMessage = emailMessage.Replace(dynamicTextItem.Key, dynamicTextItem.Value);
            }
            // Return the modfied email template string
            return emailMessage;
        }


        private void SendEmail(string email, string title, string content)
        {
            // Get ahold of the root/home node
            IPublishedContent root = Umbraco.ContentAtRoot().First();
            // Get the SMTP server
            string smtpServer = root.GetProperty("smtpServer").Value.ToString();
            // Get the SMTP port
            int smtpPort = Convert.ToInt32(root.GetProperty("smtpPort").Value);
            // Get the SMTP User Name
            string exchangeAccountUserName = root.GetProperty("exchangeAccountUserName").Value.ToString();
            // Get the SMTP Password
            string exchangeAccountPassword = root.GetProperty("exchangeAccountPassword").Value.ToString();
            // Get the SMTP email account
            string smtpEmail = root.GetProperty("smtpEmailAddress").Value.ToString();

            // Create a message
            MailMessage message = new MailMessage(smtpEmail, email, title, content);                                                      

            // Create an SMTP client object and send the message with it
            SmtpClient smtp = new SmtpClient(smtpServer, smtpPort);
            smtp.Credentials = new NetworkCredential(exchangeAccountUserName, exchangeAccountPassword);
            // Try to send the message
            smtp.Send(message); 
        }


        /// <summary>
        /// Emails the Web Administrator with a message from a member
        /// </summary>
        /// <param name="model">A Contact Form view model</param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult ContactUs(ContactFormViewModel model)
        {
            try
            {
                // Get ahold of the root/home node
                IPublishedContent root = Umbraco.ContentAtRoot().First();
                // Get the contact us email value
                string sendTo = root.GetProperty("incomingEmailAddress").Value.ToString();

                SendEmail(sendTo, model.MessageType, model.Message);

                // Set the sucess flag to true and post back to the same page
                TempData["IsSuccessful"] = true;
                return RedirectToCurrentUmbracoPage();
            }
            catch (Exception ex) // If the message failed to send
            {
                // Set the success flag to false and post back to the same page
                TempData["IsSuccessful"] = false;
                return RedirectToCurrentUmbracoPage();
            }
        }

        [HttpPost]
        public ActionResult ForgotUserName([Bind(Prefix = "forgotUserNameViewModel")]ForgotUserNameViewModel model)
        {
            try 
            {
                // Attempt to get the member based on the given email address
                var member = Services.MemberService.GetByEmail(model.Email);
                // If a member with that email exists
                if (member != null)
                {
                    // Get the Umbraco root node to access dynamic information (phone numbers, emails, ect)
                    IPublishedContent root = Umbraco.TypedContentAtRoot().First();
                   
                    // Build a dictionary for all teh dynamic text in the email template
                    Dictionary<string, string> dynamicText = new Dictionary<string,string>();
                    dynamicText.Add("<%FirstName%>", member.GetValue("firstName").ToString());
                    dynamicText.Add("<%UserName%>", member.Username);
                    dynamicText.Add("<%PhoneNumber%>", root.GetProperty("phoneNumber").Value.ToString());

                    //Get the Verification Email Template ID
                    var emailTemplateId = root.GetProperty("forgotUserNameEmailTemplate").Value;


                    SendEmail(member.Email, "Health Republic Insurance - UserName Recovery",
                                             BuildEmail((int)emailTemplateId, dynamicText));

                    // Set the sucess flag to true and post back to the same page
                    TempData["IsSuccessful"] = true;
                    return RedirectToCurrentUmbracoPage();
                }
                else // The email has no member associated with it
                {
                    // Set the success flag to false and post back to the same page
                    TempData["IsSuccessful"] = false;
                    TempData["EmailNotFound"] = true;
                    return RedirectToCurrentUmbracoPage();
                }
            }
            catch(Exception ex)
            {
                ModelState.AddModelError("forgotUserNameViewModel", ex.Message + "\n" + ex.InnerException.Message + "\n");
                // Set the success flag to false and post back to the same page
                TempData["IsSuccessful"] = false;
                return RedirectToCurrentUmbracoPage();
            }
        }

        [HttpGet]
        public bool ResetPassword(string userName)
        {
            try
            {
                var member = Membership.GetUser(userName);
                string newPass = member.ResetPassword();

                // Get the Umbraco root node to access dynamic information (phone numbers, emails, ect)
                IPublishedContent root = Umbraco.TypedContentAtRoot().First();

                // Build a dictionary for all teh dynamic text in the email template
                Dictionary<string, string> dynamicText = new Dictionary<string, string>();
                dynamicText.Add("<%FirstName%>", member.UserName);
                dynamicText.Add("<%PhoneNumber%>", root.GetProperty("phoneNumber").Value.ToString());
                dynamicText.Add("<%NewPassword%>", newPass);

                //Get the Verification Email Template ID
                var emailTemplateId = root.GetProperty("resetPasswordEmailTemplate").Value;

                SendEmail(member.Email, 
                          "Health Republic Insurance - Password Reset",
                          BuildEmail((int) emailTemplateId, dynamicText));

                return true;
            }
            catch(Exception ex)
            {
                return false;
            }
        }

        [HttpPost]
        public ActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                #region User Exists
                // If the username exists
                if (Services.MemberService.GetByUsername(model.UserName) != null)
                {
                    // Get the member
                    var member = Membership.GetUser(model.UserName);

                    // Reset the password and store it for local use
                    string newPass = member.ResetPassword();

                    // Get the Umbraco root node to access dynamic information (phone numbers, emails, ect)
                    IPublishedContent root = Umbraco.TypedContentAtRoot().First();

                    // Build a dictionary for all the dynamic text in the email template
                    Dictionary<string, string> dynamicText = new Dictionary<string, string>();
                    dynamicText.Add("<%FirstName%>", member.UserName);
                    dynamicText.Add("<%PhoneNumber%>", root.GetProperty("phoneNumber").Value.ToString());
                    dynamicText.Add("<%NewPassword%>", newPass);

                    //Get the Verification Email Template ID
                    var emailTemplateId = root.GetProperty("resetPasswordEmailTemplate").Value;

                    // Send the email with the new password
                    SendEmail(member.Email,
                                "Health Republic Insurance - Password Reset",
                                BuildEmail((int)emailTemplateId, dynamicText));

                    TempData["IsSuccessful"] = true;
                    return RedirectToCurrentUmbracoPage();
                } 
                #endregion
                #region User Not Found
                else // USERNAME DOESNT EXIST; Check if old IWS user
                {
                    #region Checkolduser
                    // Create a JSON object to receive the HRI API response
                    JObject json;
                    // Exectue a GET against the API
                    using (var client = new WebClient())
                    {
                        try
                        {
                            // Read the response into a string
                            string jsonString = client.DownloadString("http://" + Request.Url.Host + ":" + Request.Url.Port + "/umbraco/api/HriApi/GetRegisteredUserByUsername?userName=" + model.UserName);
                            // If the user existed create a JSON object
                            if (jsonString != "null")
                                json = JObject.Parse(jsonString);
                            else // There is an API error
                            {
                                //don't add a field level error, just model level
                                ModelState.AddModelError("forgotPasswordViewModel", "Sorry, that user name does not exist in our system.");
                                return CurrentUmbracoPage();
                            }
                        }
                        catch (Exception ex) // There was an error in connecting to or executing the function on the API
                        {
                            ModelState.AddModelError("forgotPasswordViewModel", "Error in API call GetRegisteredUserByUsername");
                            return CurrentUmbracoPage();
                        }
                    }

                    // If the user exists in IWS database
                    if (json["RegId"] != null)
                    {
                        // Create the registration model
                        var registerModel = Members.CreateRegistrationModel();
                        // Member Name
                        registerModel.Name = json["FirstName"].ToString() + " " + json["LastName"].ToString();
                        // Member Id
                        registerModel.MemberProperties.Where(p => p.Alias == "memberId").FirstOrDefault().Value = json["RegId"].ToString();
                        // User Name
                        registerModel.Username = json["UserName"].ToString();
                        // First Name
                        registerModel.MemberProperties.Where(p => p.Alias == "firstName").FirstOrDefault().Value = json["FirstName"].ToString();
                        // Last Name
                        registerModel.MemberProperties.Where(p => p.Alias == "lastName").FirstOrDefault().Value = json["LastName"].ToString();
                        // SSN
                        if (json["Ssn"].HasValues)
                            registerModel.MemberProperties.Where(p => p.Alias == "ssn").FirstOrDefault().Value = json["Ssn"].ToString();
                        // SSN
                        if (json["EbixId"].HasValues)
                            registerModel.MemberProperties.Where(p => p.Alias == "ebixId").FirstOrDefault().Value = json["ebixID"].ToString();
                        // Email
                        if (json["EMail"].HasValues)
                            registerModel.Email = json["EMail"].ToString();
                        // Zip Code
                        if (json["ZipCode"].HasValues)
                            registerModel.MemberProperties.Where(p => p.Alias == "zipCode").FirstOrDefault().Value = json["ZipCode"].ToString();
                        // Phone Number
                        if (json["PhoneNumber"].HasValues)
                            registerModel.MemberProperties.Where(p => p.Alias == "phoneNumber").FirstOrDefault().Value = json["PhoneNumber"].ToString();
                        // Y Number
                        if (json["MemberId"].HasValues)
                            registerModel.MemberProperties.Where(p => p.Alias == "yNumber").FirstOrDefault().Value = json["MemberId"].ToString();


                        registerModel.Password = Membership.GeneratePassword(12, 4);
                        registerModel.LoginOnSuccess = false;
                        registerModel.UsernameIsEmail = false;

                        // Register the user with Door3 automatically
                        MembershipCreateStatus status;
                        var newMember = Members.RegisterMember(registerModel, out status, registerModel.LoginOnSuccess);
                        // Force sign out (hack for Umbraco bug that automatically logs user in on registration
                        Session.Clear();
                        FormsAuthentication.SignOut();

                        // Authenticate the user automatically as a registered user
                        newMember.IsApproved = true;
                        System.Web.Security.Roles.AddUserToRole(newMember.UserName, "Registered");
                        #endregion
                        // Reset the password and send an email to the user
                        bool resetSuccess;
                        string resetApiUrl = "http://" + Request.Url.Host + ":" + Request.Url.Port + "/umbraco/Surface/EmailSurface/ResetPassword?userName=" + model.UserName;
                        using (var client = new WebClient())
                        {
                            var result = client.DownloadString(resetApiUrl);
                            resetSuccess = Convert.ToBoolean(result);
                        }
                        return RedirectToCurrentUmbracoPage();                      
                    }
                    return CurrentUmbracoPage();
                }
            
            }
            else // The model was invalid
            {
                TempData["IsSuccessful"] = false;
                return RedirectToCurrentUmbracoPage();
            }
            #endregion
        }


        /// <summary>
        /// This version is called from the resend email page. It sends them a new verification email
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult SendVerificationLink([Bind(Prefix = "sendVerificationLinkModel")]SendVerificationLinkModel model)
        {
            // If the user model is valid and the user exists
            if (ModelState.IsValid && Services.MemberService.GetByUsername(model.UserName) != null)
            {  
                // If this user has already been verified
                if(Services.MemberService.GetByUsername(model.UserName).IsApproved)
                { 
                    return Content("This account has already been verified!");
                }
                // Get a handle on the member
                var member = Services.MemberService.GetByUsername(model.UserName);               
                // Create a random Guid
                Guid key = Guid.NewGuid();
                // Update the user's Guid field
                member.SetValue("guid", key.ToString());
                // Save the updated information
                Services.MemberService.Save(member);

                // Get ahold of the root/home node
                IPublishedContent root = Umbraco.ContentAtRoot().First();
                // Get the Verification Email Template ID
                var emailTemplateId = root.GetProperty("verificationEmailTemplate").Value;

                // Build a dictionary for all the dynamic text in the email template
                Dictionary<string, string> dynamicText = new Dictionary<string, string>();
                dynamicText.Add("<%FirstName%>", member.GetValue("firstName").ToString());
                dynamicText.Add("<%PhoneNumber%>", root.GetProperty("phoneNumber").Value.ToString());
                dynamicText.Add("<%VerificationUrl%>", "http://" + Request.Url.Host + ":" + Request.Url.Port + "/umbraco/Surface/MembersSurface/ActivateUser?username=" + model.UserName + "&guid=" + key.ToString());                                

                // Try to send the message
                try
                {
                    SendEmail(member.Email, "Health Republic Insurance - Member Verification Link",
                                            BuildEmail((int)emailTemplateId, dynamicText));
                }
                catch(SmtpException ex)
                {
                    //don't add a field level error, just model level
                    ModelState.AddModelError("sendVerificationLinkModel", ex.Message + "\n" + ex.InnerException.Message + "\n");
                    return CurrentUmbracoPage();
                }

                // Mark this method as successful for the next page
                TempData["IsSuccessful"] = true;

                // If there is a redirect url
                if (model.RedirectUrl != "" && model.RedirectUrl != null)
                    // Send the user to that page
                    return Redirect(model.RedirectUrl);
                else
                    // Otherwise send the user to the home page
                    return Redirect("/");
            }
            else // Model was bad or user didnt exist
            {
                // Mark the method as failed
                TempData["IsSuccessful"] = false;
                // Return the user to the home page
                return Redirect("/");
            }
        }

        /// <summary>
        /// This is called from the registration page after a new user is registered
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult SendVerificationLink_GET(SendVerificationLinkModel model)
        {
            if (ModelState.IsValid && Services.MemberService.GetByUsername(model.UserName) != null)
            {
                // Get a handle on the member
                var member = Services.MemberService.GetByUsername(model.UserName);
                // Create a random Guid
                Guid key = Guid.NewGuid();
                // Update the user's Guid field
                member.SetValue("guid", key.ToString());
                // Save the updated information
                Services.MemberService.Save(member);

                // Get ahold of the root/home node
                IPublishedContent root = Umbraco.ContentAtRoot().First();
                // Get the Verification Email Template ID
                var emailTemplateId = root.GetProperty("verificationEmailTemplate").Value;                

                // Build a dictionary for all the dynamic text in the email template
                Dictionary<string, string> dynamicText = new Dictionary<string, string>();
                dynamicText.Add("<%FirstName%>", member.GetValue("firstName").ToString());
                dynamicText.Add("<%PhoneNumber%>", root.GetProperty("phoneNumber").Value.ToString());
                dynamicText.Add("<%VerificationUrl%>", root.GetProperty("HostUrl").Value.ToString() + "/umbraco/Surface/MembersSurface/ActivateUser?username=" + model.UserName + "&guid=" + key.ToString());

                // Try to send the message
                try
                {
                    SendEmail(member.Email, "Health Republic Insurance - Member Verification Link",
                                            BuildEmail((int)emailTemplateId, dynamicText));
                }
                catch (SmtpException ex)
                {
                    //don't add a field level error, just model level
                    ModelState.AddModelError("sendVerificationLinkModel", ex.Message + "\n" + ex.InnerException.Message + "\n");
                    return Redirect("/for-members/register");
                }

                // Mark this method as successful for the next page
                TempData["IsSuccessful"] = true;

                // If there is a redirect url
                if (model.RedirectUrl != "" && model.RedirectUrl != null)
                    // Send the user to that page
                    return Redirect(model.RedirectUrl);
                else
                    // Otherwise send the user to the home page
                    return Redirect("/");
            }
            else // Model was bad or user didnt exist
            {
                // Mark the method as failed
                TempData["IsSuccessful"] = false;
                // Return the user to the home page
                return Redirect("/");
            }
        }
    }
}