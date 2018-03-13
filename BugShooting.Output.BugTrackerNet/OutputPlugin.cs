using BS.Plugin.V3.Common;
using BS.Plugin.V3.Output;
using BS.Plugin.V3.Utilities;
using System;
using System.Drawing;
using System.Linq;
using System.Net;
using System.IO;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace BugShooting.Output.BugTrackerNet
{
  public class OutputPlugin: OutputPlugin<Output>
  {

    protected override string Name
    {
      get { return "BugTracker.NET"; }
    }

    protected override Image Image64
    {
      get  { return Properties.Resources.logo_64; }
    }

    protected override Image Image16
    {
      get { return Properties.Resources.logo_16 ; }
    }

    protected override bool Editable
    {
      get { return true; }
    }

    protected override string Description
    {
      get { return "Attach screenshots to BugTracker.NET bugs."; }
    }
    
    protected override Output CreateOutput(IWin32Window Owner)
    {
      
      Output output = new Output(Name, 
                                 String.Empty, 
                                 String.Empty, 
                                 String.Empty, 
                                 "Screenshot",
                                 FileHelper.GetFileFormats().First().ID, 
                                 true,
                                 1);

      return EditOutput(Owner, output);

    }

    protected override Output EditOutput(IWin32Window Owner, Output Output)
    {

      Edit edit = new Edit(Output);

      var ownerHelper = new System.Windows.Interop.WindowInteropHelper(edit);
      ownerHelper.Owner = Owner.Handle;
      
      if (edit.ShowDialog() == true) {

        return new Output(edit.OutputName,
                          edit.Url,
                          edit.UserName,
                          edit.Password,
                          edit.FileName,
                          edit.FileFormatID,
                          edit.OpenItemInBrowser,
                          Output.LastBugID);
      }
      else
      {
        return null; 
      }

    }

    protected override OutputValues SerializeOutput(Output Output)
    {

      OutputValues outputValues = new OutputValues();

      outputValues.Add("Name", Output.Name);
      outputValues.Add("Url", Output.Url);
      outputValues.Add("UserName", Output.UserName);
      outputValues.Add("Password",Output.Password, true);
      outputValues.Add("OpenItemInBrowser", Convert.ToString(Output.OpenItemInBrowser));
      outputValues.Add("FileName", Output.FileName);
      outputValues.Add("FileFormatID", Output.FileFormatID.ToString());
      outputValues.Add("LastBugID", Output.LastBugID.ToString());

      return outputValues;
      
    }

    protected override Output DeserializeOutput(OutputValues OutputValues)
    {

      return new Output(OutputValues["Name", this.Name],
                        OutputValues["Url", ""], 
                        OutputValues["UserName", ""],
                        OutputValues["Password", ""], 
                        OutputValues["FileName", "Screenshot"], 
                        new Guid(OutputValues["FileFormatID", ""]),
                        Convert.ToBoolean(OutputValues["OpenItemInBrowser", Convert.ToString(true)]),
                        Convert.ToInt32(OutputValues["LastBugID", "1"]));

    }

    protected override async Task<SendResult> Send(IWin32Window Owner, Output Output, ImageData ImageData)
    {

      try
      {

        string userName = Output.UserName;
        string password = Output.Password;
        bool showLogin = string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password);
        bool rememberCredentials = false;

        string fileName = AttributeHelper.ReplaceAttributes(Output.FileName, ImageData);

        while (true)
        {

          if (showLogin)
          {

            // Show credentials window
            Credentials credentials = new Credentials(Output.Url, userName, password, rememberCredentials);

            var ownerHelper = new System.Windows.Interop.WindowInteropHelper(credentials);
            ownerHelper.Owner = Owner.Handle;

            if (credentials.ShowDialog() != true)
            {
              return new SendResult(Result.Canceled);
            }

            userName = credentials.UserName;
            password = credentials.Password;
            rememberCredentials = credentials.Remember;

          }

          try
          {

            // Show send window
            Send send = new Send(Output.Url, Output.LastBugID, fileName);

            var ownerHelper = new System.Windows.Interop.WindowInteropHelper(send);
            ownerHelper.Owner = Owner.Handle;

            if (!send.ShowDialog() == true)
            {
              return new SendResult(Result.Canceled);
            }

            string title;
            if (send.CreateNewBug && 
                !string.IsNullOrEmpty(send.Comment.Trim()))
            {
              title = send.Comment;
            }
            else
            {
              title = "[NO TITLE]";
            }

            IFileFormat fileFormat = FileHelper.GetFileFormat(Output.FileFormatID);
            string fullFileName = String.Format("{0}.{1}", send.FileName, fileFormat.FileExtension);
            byte[] fileBytes = FileHelper.GetFileBytes(Output.FileFormatID, ImageData);
            string imageData = HttpUtility.UrlEncode(Convert.ToBase64String(fileBytes));

            string postData = "username=" + userName +
                              "&password=" + password +
                              "&short_desc=" + title +
                              "&attachment_content_type=" + fileFormat.MimeType +
                              "&attachment_filename=" + fullFileName +
                              "&attachment_desc=" + send.Comment +
                              "&attachment=" + imageData;
            if (!send.CreateNewBug)
            {
                postData += "&bugid=" + send.BugID.ToString();
            }

            HttpWebRequest request = (HttpWebRequest)(WebRequest.Create(Output.Url + "/insert_bug.aspx"));
            request.ContentType = "application/x-www-form-urlencoded";
            request.Method = "POST";

            Byte[] postBytes = System.Text.Encoding.UTF8.GetBytes(postData);
            request.ContentLength = postBytes.Length;

            using (Stream requestStream = await request.GetRequestStreamAsync())
            {
              requestStream.Write(postBytes, 0, postBytes.Length);
              requestStream.Close();
            }

            using (HttpWebResponse response = (HttpWebResponse)(await request.GetResponseAsync()))
            {

              if (response.StatusCode == HttpStatusCode.OK)
              {

                using (Stream responseStream = response.GetResponseStream())
                {

                  string responseString;
                  using (StreamReader reader = new StreamReader(responseStream))
                  {
                    responseString = reader.ReadToEnd();
                  }

                  if (responseString.ToLower().StartsWith("ok"))
                  {

                    int bugID = Convert.ToInt32(responseString.Substring(3));

                    // Open issue in browser
                    if (Output.OpenItemInBrowser)
                    {
                      WebHelper.OpenUrl(String.Format("{0}/edit_bug.aspx?id={1}", Output.Url, bugID));
                    }

                    return new SendResult(Result.Success,
                                          new Output(Output.Name,
                                                     Output.Url,
                                                     (rememberCredentials) ? userName : Output.UserName,
                                                     (rememberCredentials) ? password : Output.Password,
                                                     Output.FileName,
                                                     Output.FileFormatID,
                                                     Output.OpenItemInBrowser,
                                                     bugID));
                  }

                }

              }

            }

          }
          catch (WebException ex)
          {
            return new SendResult(Result.Failed, ex.Message);
          }
          catch
          {
            // NOP
          }

          // Login failed
          showLogin = true;

        }

      }
      catch (Exception ex)
      {
        return new SendResult(Result.Failed, ex.Message);
      }

    }
      
  }
}
