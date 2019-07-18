using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Drawing;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models;
using System.Net;
using System.IO;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using System.Text;

namespace AppSANA
{
    public static class Program
    {
        private const string subscriptionKey = "6d98a5fea07a4c9ea321f141d0dd8a03";
        private const int numberOfCharsInOperationId = 36;
        static void Main(string[] args)
        {
            Console.WriteLine("DEMO Custom Vision SANA POSESION");
            Console.WriteLine();
            ProcessAsync().GetAwaiter().GetResult();

            Console.WriteLine("Presione cualquier tecla para salir de la aplicación.");
            Console.ReadLine();
        }

        private static async Task ProcessAsync()
        {
            CloudStorageAccount storageAccount = null;
            CloudBlobContainer cloudBlobContainer = null;

            string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=sasanaposesion;AccountKey=TD13Kp2fDH8Ao9wITisGGjt0Z9ax6llBvNaJTyd/IgNh0SM12XWRqQvmCKVmRppvRO0mRKtrOtFc5iUw5yrgrg==;EndpointSuffix=core.windows.net";

            if (CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
            {
                try
                {
                    CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

                    cloudBlobContainer = cloudBlobClient.GetContainerReference("sanaposesion");

                    Console.WriteLine("1. Obteniendo listado de Blobs del contenedor.");
                    DateTime dateForSearch = DateTime.Now.AddDays(-1);

                    var result = await cloudBlobContainer.ListBlobsSegmentedAsync("Procesar/" + dateForSearch.ToString("yyyyMMdd"), true, BlobListingDetails.None, 5000, null, null, null);
                    var blobs = result.Results;

                    Console.WriteLine("1.1. Buscando y obteniendo proyectos necesarios Custom Vision.");
                    Project projectForm = Training.GetProject("FORM-TEST");
                    Project projectSignature = Training.GetProject("Signature-TEST");

                    StringBuilder csvcontent = new StringBuilder();
                    String csvpath = "C:/Users/Kevin Sanchez/ReportSANAPOSESIONLOTE" + dateForSearch.ToString("yyyyMMdd") + ".csv";

                    // Nombre de los modelos publicados de cada proyecto
                    String modelNameObjectDetection = "TestDemoFormIT2";

                    //Console.WriteLine("** Total de archivos a procesar = " + (IListBlobItem) blobs.;
                    int count = 0;
                    //Recorriendo blobs de la carpeta del dia anterior
                    foreach (var blob in blobs)
                    {
                        Console.WriteLine("##############################################################");
                        count = count + 1;

                        var blobCloud = (CloudBlockBlob)blob;
                        var numberBlob = blobCloud.Name.Split("/")[2].Split(".")[0];

                        //Console.WriteLine(blobCloud.Uri.ToString() + "," + numberBlob);

                        Console.WriteLine("2. Conectando al proyecto de Formato de Sana Posesion");

                        Dictionary<string, float[]> imagePrediction = await Prediction.PredictImageURLForm(projectForm.Id, modelNameObjectDetection, blobCloud.Uri.ToString());

                        Console.WriteLine("2*. Conexion Exitosa");

                        // 2. Descargando Blob de formulario
                        bool processBool = await DownloadLocalBlob(numberBlob, blobCloud.Uri.ToString());

                        if (processBool)
                        {
                            Console.WriteLine("3. Obteniendo puntos aproximados para recorte de campos.");
                            var respCrop = await ProcessBlob(numberBlob, imagePrediction);

                            if (respCrop) { Console.WriteLine("3* Campos localizados."); }

                            Console.WriteLine("4. Fuentes listas para realizar conexion y prediccion de campos de CC y Firma.");
                            var respID = await CVIDAsync(numberBlob);

                            var respDictionary = await CVSignatureAsync(respID, numberBlob, projectSignature);

                            Console.WriteLine("5. Fin de procesamiento de archivo.");

                            bool isEmpty = (respDictionary.Count == 0);

                            if (!isEmpty) {
                                foreach (var item in respDictionary)
                                {
                                    csvcontent.AppendLine($"{numberBlob},{item.Value:P1}," + item.Key + "," + respID);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("Error en el Blob Storage no se pueden obtener los documentos.");
                        }
                    }
                    Console.WriteLine("** Documento CSV alojado en:" + csvpath);
                    File.AppendAllText(csvpath, csvcontent.ToString());
                    Console.WriteLine();
                }
                catch (StorageException ex)
                {
                    Console.WriteLine("Error devuelto por el servicio: {0}", ex.Message);
                }
            }
            else
            {
                Console.WriteLine("Una cadena de conexión no se ha definido.");
            }
        }

        private static async Task<Dictionary<string, double>> CVSignatureAsync(string respID, string numberBlob, Project projectSignature)
        {
            Dictionary<string, double> logPredictionSignature = new Dictionary<string, double>();
            string localFileNameFirma = @"C:\DEMOAGRARIO\" + "Firma_Presidente_JAC-" + numberBlob + ".png";

            try
            {
                logPredictionSignature = await Prediction.PredictImageFile(projectSignature.Id, "TestSignatureIT3", localFileNameFirma, respID);

                return logPredictionSignature;
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n{e.GetType().Name}: Error en proyecto de firmas.");
            }

            return logPredictionSignature;
        }

        public static async Task<Boolean> DownloadLocalBlob(string blobName, string uri)
        {
            //Console.WriteLine("\nCache de imagen...");

            try
            {
                string localFilename = @"C:\DEMOAGRARIO\" + blobName + ".jpg";
                if (!File.Exists(localFilename))
                {
                    using (WebClient client = new WebClient())
                    {
                        //Console.WriteLine("\nImagen descargada...");
                        client.DownloadFile(uri, localFilename);
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n{e.GetType().Name}: No se pudo descargar las imagenes.");
                return false;
            }
        }

        public static async Task<Boolean> ProcessBlob(string blobName, Dictionary<string, float[]> coordinatesImages)
        {
            //Console.WriteLine("\nIniciando recorte de Firma y ID...");
            bool resp = false;

            try
            {
                string localFilename = @"C:\DEMOAGRARIO\" + blobName + ".jpg";
                if (File.Exists(localFilename))
                {
                    System.Drawing.Image imageBlobForm = System.Drawing.Image.FromFile(localFilename);
                    //Rectangle cropRectangleFirmaPresident = new Rectangle(430, 980, 700, 270);
                    //Rectangle cropRectangleIDPresident = new Rectangle(450, 1420, 680, 160);

                    Rectangle cropRectangleFirmaPresident = new Rectangle(930, 2180, 1600, 650);
                    Rectangle cropRectangleIDPresident = new Rectangle(1030, 3200, 1400, 400);

                    foreach (var item in coordinatesImages)
                    {
                        var nameBlobCrop = item.Key;
                        float[] cropCoordinates = item.Value;

                        //await CropImage(imageBlobForm, nameBlobCrop, blobName, cropRectangleFirmaPresident);
                        //await CropImage(imageBlobForm, nameBlobCrop, blobName, cropRectangleIDPresident);

                        if (nameBlobCrop == "Firma_Presidente_JAC" || nameBlobCrop == "ID_Presidente_JAC")
                        {
                            if (nameBlobCrop == "Firma_Presidente_JAC")
                            {
                                await CropImage(imageBlobForm, nameBlobCrop, blobName, cropRectangleFirmaPresident);
                            }                            
                            else
                            {
                                await CropImage(imageBlobForm, nameBlobCrop, blobName, cropRectangleIDPresident);
                            }
                        }
                    }
                    resp = true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n{e.GetType().Name}: No se han podido recortar las imagenes del formulario.");
            }
            return resp;
        }

        public static async Task CropImage(System.Drawing.Image imagecrop, string nameCrop, string nameFile, Rectangle cropRect)
        {
            string localFilename = @"C:\DEMOAGRARIO\" + nameCrop + "-" + nameFile + ".png";

            Bitmap bmpImage = new Bitmap(imagecrop);
            bmpImage.Clone(cropRect, bmpImage.PixelFormat).Save(localFilename);
        }

        public static async Task<string> CVIDAsync(string blobName)
        {
            //Console.WriteLine("\nIniciando la identificacion de firma del presidente...");
            string IDForm = "";

            try
            {
                string localFileNameID = @"C:\DEMOAGRARIO\" + "ID_Presidente_JAC-" + blobName + ".png";
                string localFileNameFirma = @"C:\DEMOAGRARIO\" + "Firma_Presidente_JAC-" + blobName + ".png";

                if (File.Exists(localFileNameID) && File.Exists(localFileNameFirma))
                {
                    ComputerVisionClient computerVision = new ComputerVisionClient(
                        new ApiKeyServiceClientCredentials(subscriptionKey),
                        new System.Net.Http.DelegatingHandler[] { });

                    computerVision.Endpoint = "https://eastus2.api.cognitive.microsoft.com/";
                    //Console.WriteLine("Imágenes siendo analizadas ...");

                    IDForm = await ExtractLocalTextAsync(computerVision, localFileNameID);
                    IDForm = string.Join("", IDForm.ToUpper().Split(" "));
                    IDForm = IDForm.Replace('S', '5');
                    IDForm = IDForm.Replace('I', '1');
                    IDForm = IDForm.Replace('G', '6');

                    return IDForm;

                    // Identificacion de Firma en Base al ID del presidente
                    //respPrediction = await Prediction.PredictImageFile(guidProject.Id, "TestFirmasDemo", localFileNameFirma, IDForm);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n{e.GetType().Name}: No se ha podido reconocer el ID y las firma de la JAC.");
            }

            return IDForm;
        }

        // Recognize text from a local image
        private static async Task<string> ExtractLocalTextAsync(ComputerVisionClient computerVision, string imagePath)
        {
            using (Stream imageStream = File.OpenRead(imagePath))
            {
                // Start the async process to recognize the text
                BatchReadFileInStreamHeaders textHeaders =
                    await computerVision.BatchReadFileInStreamAsync(
                        imageStream);

                string resultText = await GetTextAsync(computerVision, textHeaders.OperationLocation);

                return resultText;
            }
        }

        // Retrieve the recognized text
        private static async Task<string> GetTextAsync(ComputerVisionClient computerVision, string operationLocation)
        {
            // Retrieve the URI where the recognized text will be
            // stored from the Operation-Location header
            string operationId = operationLocation.Substring(
                operationLocation.Length - numberOfCharsInOperationId);

            //Console.WriteLine("\nCalling GetHandwritingRecognitionOperationResultAsync()");
            ReadOperationResult result =
                await computerVision.GetReadOperationResultAsync(operationId);

            // Wait for the operation to complete
            int i = 0;
            int maxRetries = 10;
            while ((result.Status == TextOperationStatusCodes.Running ||
                    result.Status == TextOperationStatusCodes.NotStarted) && i++ < maxRetries)
            {
                //Console.WriteLine("Server status: {0}, waiting {1} seconds...", result.Status, i);
                await Task.Delay(1000);

                result = await computerVision.GetReadOperationResultAsync(operationId);
            }

            // Display the results
            //Console.WriteLine();
            var recResults = result.RecognitionResults;
            string concat = "";
            foreach (TextRecognitionResult recResult in recResults)
            {
                foreach (Line line in recResult.Lines)
                {
                    //Console.WriteLine(line.Text);
                    concat = concat + line.Text;
                }
            }
            return concat;
        }
    }
}
