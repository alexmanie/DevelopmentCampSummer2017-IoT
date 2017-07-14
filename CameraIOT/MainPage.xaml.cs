using Emmellsoft.IoT.Rpi.SenseHat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace CameraIOT
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        MediaCapture mediaCapture = new MediaCapture();
        MediaElement mediaElement = new MediaElement();
        ISenseHat senseHat;
        const string PHOTO_FILE_NAME = "photoName.jpg";
        const string subscriptionKey = "620ce5edd527494881df952d5a62b7b2";
        const string uriBase = "https://westeurope.api.cognitive.microsoft.com/vision/v1.0/analyze";
        public MainPage()
        {
            this.InitializeComponent();
            this.label.Text = string.Empty;

            Task.Run(async () => await MakeAnalysisRequest());

            DispatcherTimer timer = new DispatcherTimer();
            timer.Tick += TickTimer;
            timer.Interval = TimeSpan.FromSeconds(0.5);
            senseHat = Task.Run(async () => await SenseHatFactory.GetSenseHat().ConfigureAwait(false)).Result;
            senseHat.Display.Clear();
            senseHat.Display.Update();
            var devices = Task.Run(async () => await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture));
            var captureInitSettings = new MediaCaptureInitializationSettings();
            captureInitSettings.StreamingCaptureMode = StreamingCaptureMode.AudioAndVideo;
            captureInitSettings.PhotoCaptureSource = PhotoCaptureSource.Photo;
            InitializeCamera();

            timer.Start();
        }

        private async void TickTimer(object sender, object e)
        {
            if (senseHat.Joystick.Update())
            {
                bool currentPressingEnter = senseHat.Joystick.EnterKey == KeyState.Pressing;
                if (currentPressingEnter)
                {
                    await TakePhoto();
                    await MakeAnalysisRequest();
                }

            }
        }
        private async void InitializeCamera()
        {
            await mediaCapture.InitializeAsync();

        }

        private async Task TakePhoto()
        {
            this.label.Text = string.Empty;

            StorageFolder storageFolder = KnownFolders.PicturesLibrary;

            var photoFile = await storageFolder.CreateFileAsync(PHOTO_FILE_NAME, CreationCollisionOption.ReplaceExisting);

            //await photoFile.CopyAsync(storageFolder, photoFile.Name, NameCollisionOption.ReplaceExisting);
            ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();
            await mediaCapture.CapturePhotoToStorageFileAsync(imageProperties, photoFile);


            IRandomAccessStream photoStream = await photoFile.OpenReadAsync();
            BitmapImage bitmap = new BitmapImage();
            bitmap.SetSource(photoStream);
            captureImage.Source = bitmap;

        }

        private async Task readText(string mytext)
        {
            MediaElement mediaplayer = new MediaElement();
            using (var speech = new SpeechSynthesizer())
            {
                speech.Voice = SpeechSynthesizer.AllVoices.First(gender => gender.Gender == VoiceGender.Male);
                string ssml = @"<speak version='1.0' " + "xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>" + mytext + "</speak>";
                SpeechSynthesisStream stream = await speech.SynthesizeSsmlToStreamAsync(ssml);
                mediaplayer.SetSource(stream, stream.ContentType);
                mediaplayer.Play();
            }
        }

        public async Task<byte[]> ReadFile(StorageFile file)
        {
            byte[] fileBytes = null;
            using (IRandomAccessStreamWithContentType stream = await file.OpenReadAsync())
            {
                fileBytes = new byte[stream.Size];
                using (DataReader reader = new DataReader(stream))
                {
                    await reader.LoadAsync((uint)stream.Size);
                    reader.ReadBytes(fileBytes);
                }
            }

            return fileBytes;
        }

        async Task MakeAnalysisRequest()
        {
            HttpClient client = new HttpClient();

            // Request headers.
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

            // Request parameters. A third optional parameter is "details".
            string requestParameters = "visualFeatures=Categories,Description,Color&language=en";

            // Assemble the URI for the REST API Call.
            string uri = uriBase + "?" + requestParameters;

            HttpResponseMessage response;
            var image = await KnownFolders.PicturesLibrary.GetFileAsync(PHOTO_FILE_NAME);

            // Request body. Posts a locally stored JPEG image.
            byte[] byteData = Task.Run(async () => await ReadFile(image)).Result;

            using (ByteArrayContent content = new ByteArrayContent(byteData))
            {
                // This example uses content type "application/octet-stream".
                // The other content types you can use are "application/json" and "multipart/form-data".
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                // Execute the REST API call.
                response = await client.PostAsync(uri, content);

                // Get the JSON response.
                string contentString = await response.Content.ReadAsStringAsync();
                JObject objResponse = JsonConvert.DeserializeObject<JObject>(contentString);

                JArray jArray = (JArray)objResponse["description"]["captions"];
                JToken textObj = jArray[0];
                string text = textObj.Value<string>("text");
                this.label.Text = text;

                await readText(text);
            }
        }


    }
}
