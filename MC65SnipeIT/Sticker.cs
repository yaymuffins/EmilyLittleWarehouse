﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Net;

using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using System.Collections;
using System.Collections.Specialized;


using ZXing;
using ZXing.Common;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MC65SnipeIT
{
    public partial class Sticker : Form
    {
        enum RasterOperation : uint { SRC_COPY = 0x00CC0020 }

        [DllImport("coredll.dll")]
        static extern int BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, RasterOperation rasterOperation);

        [DllImport("coredll.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("coredll.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        public Sticker()
        {
            InitializeComponent();
            if (MobileConfiguration.Settings["NyaPrint"] == "TRUE")
            {
                PrintBtn.Visible = true;
                SaveBtn.Location = new System.Drawing.Point(169, 540);
                this.ExitBtn.Location = new System.Drawing.Point(16, 540);
            }
            else
            {
                //change nothing
            }
            
        }
 


        private Bitmap Encode(string text, BarcodeFormat format)
        {
            var writer = new BarcodeWriter { Format = format };
            return writer.Write(text);
        }

        private void GenerateBtn_Click(object sender, EventArgs e)
        {
            string URLToEncode;
            URLToEncode = MobileConfiguration.Settings["ServerAddress"] + "/hardware/" + textBox1.Text;
            var format = BarcodeFormat.QR_CODE;
            var bitmap = Encode(URLToEncode, format);
            picBarcode.Image = bitmap;


            try
            {
                string WEBSERVICE_URL = MobileConfiguration.Settings["ServerAddress"] + "/api/v1/hardware/" + textBox1.Text;
                var webRequest = System.Net.WebRequest.Create(WEBSERVICE_URL);
                webRequest.Method = "GET";
                webRequest.Timeout = 20000;
                webRequest.ContentType = "application/json";
                webRequest.Headers.Add("Authorization", "Bearer " + MobileConfiguration.Settings["ServerKey"]);
                using (System.IO.Stream s = webRequest.GetResponse().GetResponseStream())
                {
                    using (System.IO.StreamReader sr = new System.IO.StreamReader(s))

                        try
                        {
                            {
                                var jsonResponse = sr.ReadToEnd();
                                JObject RootJson = JObject.Parse(jsonResponse);
                                string NameOfObjectToClean = RootJson["name"].ToString();
                                string NameOfObjectToClean2 = NameOfObjectToClean.Replace("\"", "");
                                string NameOfObject = NameOfObjectToClean2.Replace("quot;", "\"");
                                string AssetTagObjectToClean = RootJson["asset_tag"].ToString();
                                string AssetTagObject = AssetTagObjectToClean.Replace("\"", "");
                                string LocationArray = RootJson["rtd_location"].ToString();
                                JObject LocationArrayJson = JObject.Parse(LocationArray);
                                string LocationNameToClean = LocationArrayJson["name"].ToString();
                                string LocationNameToClean2 = LocationNameToClean.Replace("\"", "");
                                string LocationName = LocationNameToClean2.Replace("quot;", "\"");
                                NameText.Text = NameOfObject;
                                AssetText.Text = "Asset Tag: " + AssetTagObject;
                                LocationText.Text = "Location: " + LocationName;

                            }
                        }
                        catch (Exception)
                        { MessageBox.Show("Are you sure this item exist?"); }
                }
            }
            catch (Exception)
            { MessageBox.Show("Couldn't contact the server"); }

            if (MobileConfiguration.Settings["NyaPrint"] == "TRUE")
            {
                PrintBtn.Enabled = true;
                SaveBtn.Enabled = true;
            }
            else
            {
                SaveBtn.Enabled = true;
            }
        }

        private void SaveBtn_Click(object sender, EventArgs e)
        {
            try
            {
                // screenshot

                string filename = "\\" + MobileConfiguration.Settings["Storage"] + "\\ELW-Labels\\" + textBox1.Text + "-label.jpg";
                if (File.Exists(filename))
                {
                    File.Delete(filename);
                }
                Rectangle bounds = panel1.Bounds;
                IntPtr hdc = GetDC(IntPtr.Zero);
                Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format16bppRgb565);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    IntPtr dstHdc = graphics.GetHdc();
                    BitBlt(dstHdc, 0, 0, bounds.Width, bounds.Height, hdc, 36, 98,
                    RasterOperation.SRC_COPY);
                    graphics.ReleaseHdc(dstHdc);
                }
                bitmap.Save(filename, ImageFormat.Jpeg);
                ReleaseDC(IntPtr.Zero, hdc);
                MessageBox.Show("Label saved!");
            }
            catch (Exception) { MessageBox.Show("ERROR: Could not save"); }

            textBox1.Focus();
            textBox1.SelectionStart = 0;
            textBox1.SelectionLength = textBox1.Text.Length;

        }
        public static byte[] ReadAllBytes(string path)
        {
            byte[] data;
            using (FileStream screenshotdata = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                int offset = 0;
                int count = (int)screenshotdata.Length;
                data = new byte[count];
                while (count > 0)
                {
                    int bytesRead = screenshotdata.Read(data, offset, count);
                    offset += bytesRead;
                    count -= bytesRead;
                }
            }
            return data;
        }

        public static class FormUpload
        {
            private static readonly Encoding encoding = Encoding.UTF8;
            public static HttpWebResponse MultipartFormPost(string postUrl, string userAgent, Dictionary<string, object> postParameters)
            {
                string formDataBoundary = String.Format("----------{0:N}", Guid.NewGuid());
                string contentType = "multipart/form-data; boundary=" + formDataBoundary;

                byte[] formData = GetMultipartFormData(postParameters, formDataBoundary);

                return PostForm(postUrl, userAgent, contentType, formData);
            }
            private static HttpWebResponse PostForm(string postUrl, string userAgent, string contentType, byte[] formData)
            {
                HttpWebRequest request = WebRequest.Create(postUrl) as HttpWebRequest;

                if (request == null)
                {
                    throw new NullReferenceException("request is not a http request");
                }

                // Set up the request properties.  
                request.Method = "POST";
                request.ContentType = contentType;
                request.UserAgent = userAgent;
                request.ContentLength = formData.Length;

                // Send the form data to the request.  
                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(formData, 0, formData.Length);
                    requestStream.Close();
                }

                return request.GetResponse() as HttpWebResponse;
            }

            private static byte[] GetMultipartFormData(Dictionary<string, object> postParameters, string boundary)
            {
                Stream formDataStream = new System.IO.MemoryStream();
                bool needsCLRF = false;

                foreach (var param in postParameters)
                {

                    if (needsCLRF)
                        formDataStream.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));

                    needsCLRF = true;

                    if (param.Value is FileParameter) // to check if parameter if of file type   
                    {
                        FileParameter fileToUpload = (FileParameter)param.Value;

                        // Add just the first part of this param, since we will write the file data directly to the Stream  
                        string header = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\"\r\nContent-Type: {3}\r\n\r\n",
                            boundary,
                            param.Key,
                            fileToUpload.FileName ?? param.Key,
                            fileToUpload.ContentType ?? "application/octet-stream");

                        formDataStream.Write(encoding.GetBytes(header), 0, encoding.GetByteCount(header));

                        // Write the file data directly to the Stream, rather than serializing it to a string.  
                        formDataStream.Write(fileToUpload.File, 0, fileToUpload.File.Length);
                    }
                    else
                    {
                        string postData = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}",
                            boundary,
                            param.Key,
                            param.Value);
                        formDataStream.Write(encoding.GetBytes(postData), 0, encoding.GetByteCount(postData));
                    }
                }

                // Add the end of the request.  Start with a newline  
                string footer = "\r\n--" + boundary + "--\r\n";
                formDataStream.Write(encoding.GetBytes(footer), 0, encoding.GetByteCount(footer));

                // Dump the Stream into a byte[]  
                formDataStream.Position = 0;
                byte[] formData = new byte[formDataStream.Length];
                formDataStream.Read(formData, 0, formData.Length);
                formDataStream.Close();

                return formData;
            }

            public class FileParameter
            {
                public byte[] File { get; set; }
                public string FileName { get; set; }
                public string ContentType { get; set; }
                public FileParameter(byte[] file) : this(file, null) { }
                public FileParameter(byte[] file, string filename) : this(file, filename, null) { }
                public FileParameter(byte[] file, string filename, string contenttype)
                {
                    File = file;
                    FileName = filename;
                    ContentType = contenttype;
                }
            }
        }  

        private void PrintBtn_Click(object sender, EventArgs e)
        {
            // screenshot

            try
            {
                string filename = "\\" + MobileConfiguration.Settings["Storage"] + "\\ELW-Labels\\" + textBox1.Text + "-label.jpg";
                if (File.Exists(filename))
                {
                    File.Delete(filename);
                }
                Rectangle bounds = panel1.Bounds;
                IntPtr hdc = GetDC(IntPtr.Zero);
                Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format16bppRgb565);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    IntPtr dstHdc = graphics.GetHdc();
                    BitBlt(dstHdc, 0, 0, bounds.Width, bounds.Height, hdc, 36, 98,
                    RasterOperation.SRC_COPY);
                    graphics.ReleaseHdc(dstHdc);
                }
                bitmap.Save(filename, ImageFormat.Jpeg);
                ReleaseDC(IntPtr.Zero, hdc);

                // print


                string NYAPRINT_URL = MobileConfiguration.Settings["NyaPrintServer"];
                var webRequest = System.Net.WebRequest.Create(NYAPRINT_URL);
                webRequest.Method = "POST";
                webRequest.Timeout = 20000;
                string boundary = "------------------------------" + DateTime.Now.Ticks.ToString("x");
                webRequest.ContentType = "multipart/form-data; boundary=" + boundary;
                string requestURL = MobileConfiguration.Settings["NyaPrintServer"];
                byte[] bytes = ReadAllBytes(filename);
                Dictionary<string, object> postParameters = new Dictionary<string, object>();
                // Add your parameters here  
                postParameters.Add("image", new FormUpload.FileParameter(bytes, Path.GetFileName(filename), "application/object-stream"));
                string userAgent = "Someone";

                HttpWebResponse webResponse = FormUpload.MultipartFormPost(requestURL, userAgent, postParameters);
                // Process response  
                string returnResponseText;
                StreamReader responseReader = new StreamReader(webResponse.GetResponseStream());
                returnResponseText = responseReader.ReadToEnd();
                webResponse.Close();
                MessageBox.Show("Label and headpats sent to the printer");
            }
            catch (Exception) { MessageBox.Show("Printing Error"); }

            textBox1.Focus();
            textBox1.SelectionStart = 0;
            textBox1.SelectionLength = textBox1.Text.Length;



        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                GenerateBtn_Click(this, new EventArgs());
                textBox1.SelectionStart = 0;
                textBox1.SelectionLength = textBox1.Text.Length;
            }
        }

        private void ExitBtn_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}