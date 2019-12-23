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
using System.Net.Mail;
using System.Net.Mime;

namespace AppSANA
{
    public static class Program
    {
        static void Main(string[] args)
        {
            EmptyFolderTemp();
            Console.WriteLine("PoC - SANA POSESION");
            Console.WriteLine();
            ProcessAsync().GetAwaiter().GetResult();

            Console.WriteLine("Presione cualquier tecla para salir de la aplicación.");
            Console.ReadLine();
        }

        private static async Task ProcessAsync()
        {
            // Conexion Blob Storage
            CloudBlobContainer containerBlob = ConnectionBlob();

            Console.WriteLine("1. Obteniendo listado de Blobs  del dia anterior.");
            DateTime dateForSearch = DateTime.Now.AddDays(-1);

            CloudBlobDirectory result = containerBlob.GetDirectoryReference(Environment.GetEnvironmentVariable("containerPreProcessingName") + "/" + dateForSearch.ToString("yyyyMMdd"));
            bool bReadDat = ReadFileDat(result);

            if (bReadDat)
            {
                List<List<String>> resultBlobsTrans = ProcessingBlobsListStage1(result);
                // Obtener porcentaje de firma
                await ProcessingBlobsListStage2Async(resultBlobsTrans);
                // Guardar Archivo en CSV y Envio de Correo Electronico
                await SendEmailCSV();

            }
            else { Console.WriteLine("Revise la lectura del archivo .dat, "); }
        }

        /// <summary>
        /// Conexion a Blob Storage de Azure
        /// </summary>
        /// <returns></returns>
        private static CloudBlobContainer ConnectionBlob()
        {
            CloudStorageAccount storageAccount = null;
            CloudBlobContainer cloudBlobContainer = null;

            //string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=sasanaposesion;AccountKey=TD13Kp2fDH8Ao9wITisGGjt0Z9ax6llBvNaJTyd/IgNh0SM12XWRqQvmCKVmRppvRO0mRKtrOtFc5iUw5yrgrg==;EndpointSuffix=core.windows.net";
            string storageConnectionString = Environment.GetEnvironmentVariable("storageConnectionString");

            if (CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
            {
                try
                {
                    // Conexion a Blob Storage y Contenedores
                    CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
                    cloudBlobContainer = cloudBlobClient.GetContainerReference(Environment.GetEnvironmentVariable("blobContainerName"));

                    return cloudBlobContainer;
                }
                catch (StorageException ex)
                {
                    Console.WriteLine("Error del servicio: {0}", ex.Message);
                    return null;
                }
            }
            else
            {
                Console.WriteLine("Cadena de conexión a la Cuenta de Almacenamiento no se ha especificado.");
                return null;
            }
        }

        /// <summary>
        /// Lectura de Archivo .DAT 
        /// </summary>
        /// <param name="blobList"></param>
        /// <returns></returns>
        private static bool ReadFileDat(CloudBlobDirectory blobList)
        {
            // Conexion a la API de los proyectos de Custom Vision (Firmas)
            Console.WriteLine("0. Mapeando archivo .DAT");
            List<string[]> result = new List<string[]>();
            InfoFilesImages.GlobalListInfo = result;
            var blobs = blobList.ListBlobsSegmentedAsync(true, BlobListingDetails.None, 5000, null, null, null).Result;

            foreach (var item in blobs.Results)
            {
                var blobCloud = (CloudBlockBlob)item;
                var splitted = new List<string>();

                // Extension Archivo
                string ext = blobCloud.Name.Split("/")[2].Split(".")[1];

                // AGREGAR .DAT (CAMBIO)
                if (ext == "dat")
                {
                    string fileList = GetCSV(blobCloud.Uri.ToString());
                    string[] tempStr;

                    tempStr = fileList.Split("\r\n");

                    foreach (string line in tempStr)
                    {
                        if (!string.IsNullOrWhiteSpace(line)){ result.Add(line.Trim().Split("|"));}
                    }
                    InfoFilesImages.GlobalListInfo = result;
                }
            }
            if (InfoFilesImages.GlobalListInfo.Count > 0){ return true;}
            else{ return false;}
        }

        private static List<List<String>> ProcessingBlobsListStage1(CloudBlobDirectory blobList)
        {
            // Conexion a la API de los proyectos de Custom Vision (Firmas)
            //Console.WriteLine("1.1. Conectando a proyectos de Custom Vision.");
            List<List<string>> result = new List<List<string>>();
            var blobs = blobList.ListBlobsSegmentedAsync(true, BlobListingDetails.None, 5000, null, null, null).Result;

            foreach (var item in blobs.Results)
            {
                var blobCloud = (CloudBlockBlob)item;
                var listBlob = new List<string>();

                // Extension Archivo
                string ext = blobCloud.Name.Split("/")[2].Split(".")[1];

                if (ext == "jpg")
                {
                    //1. Nombre Original (Int)
                    listBlob.Add(blobCloud.Name.Split("/")[2].Split(".")[0]);

                    //2. Extension Archivo
                    listBlob.Add(blobCloud.Name.Split("/")[2].Split(".")[1]);

                    //3. Nombre Cambiado (Id Tramite) (Int)
                    listBlob.Add(blobCloud.Uri.ToString());

                    //4. URL (String)
                    listBlob.Add(blobCloud.Uri.ToString());

                    // .. Descargando Blob de formulario
                    bool processBool = DownloadLocalBlob(listBlob[0], blobCloud.Uri.ToString());

                    //5. Download (Bool) 
                    listBlob.Add(processBool.ToString());

                    //6. .DAT INFO 
                    string resultInfoImage = InfoFilesImages.getInfoOfImage(listBlob[0]);

                    if (resultInfoImage.Length > 0) { listBlob.Add(resultInfoImage); }
                    else { listBlob.Add("No hay informacion de este archivo"); }

                    result.Add(listBlob);
                }
            }
            return result;
        }

        /// <summary>
        /// Descargar en cache Blobs de Cuenta de Almacenamiento
        /// </summary>
        /// <param name="blobName"></param>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static Boolean DownloadLocalBlob(string blobName, string uri)
        {
            //Console.WriteLine("\nCache de imagen...");
            try
            {
                string localFilename = Environment.GetEnvironmentVariable("localFilePath") + blobName + ".jpg";
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
                Console.WriteLine($"\n{e.GetType().Name}: No se pudo obtener la imagen.");
                return false;
            }
        }

        private static async Task ProcessingBlobsListStage2Async(List<List<string>> blobList)
        {
            Project projectForm = Training.GetProject(Environment.GetEnvironmentVariable("projectFormName"));
            String modelNameObjectDetection = Environment.GetEnvironmentVariable("modelNameObjectDetectionForm");
            Project projectSignature = Training.GetProject(Environment.GetEnvironmentVariable("projectSignatureName"));

            foreach (List<string> subList in blobList)
            {
                if (bool.Parse(subList[4]))
                {
                    Console.WriteLine("#############");
                    Console.WriteLine(subList[0]);
                    Dictionary<string, float[]> imagePrediction = await Prediction.PredictImageURLForm(projectForm.Id, modelNameObjectDetection, subList[2]);
                    var respCrop = ProcessCropBlob(subList[0], imagePrediction);

                    //Console.WriteLine("4. Conexion y prediccion de campos de CC y Firma.");

                    var respID = await CVIDAsync(subList[0]);
                    subList.Add(respID);

                    var respDictionary = await CVSignatureAsync(respID, subList[0], projectSignature);
                    bool isEmpty = (respDictionary.Count == 0);

                    if (!isEmpty)
                    {
                        foreach (var item in respDictionary){ subList.Add(item.Key + "," + $"{item.Value:P1}");}
                    }
                    else{ subList.Add("null,null");}

                    //Console.WriteLine("5. Fin de procesamiento de archivo."); 
                }
            }
            InfoFilesImages.GeneralListPrint = blobList;
        }

        /// <summary>
        /// Coordenadas Campos formato de SANA POSESION
        /// </summary>
        /// <param name="blobName"></param>
        /// <param name="coordinatesImages"></param>
        /// <returns></returns>
        public static bool ProcessCropBlob(string blobName, Dictionary<string, float[]> coordinatesImages)
        {
            bool resp = false;

            try
            {
                string localFilename = Environment.GetEnvironmentVariable("localFilePath") + blobName + ".jpg";
                if (File.Exists(localFilename))
                {
                    System.Drawing.Image imageBlobForm = System.Drawing.Image.FromFile(localFilename);

                    Rectangle cropRectangleFirmaPresident = new Rectangle(930, 2180, 1600, 650);
                    Rectangle cropRectangleIDPresident = new Rectangle(1030, 3200, 1400, 400);

                    foreach (var item in coordinatesImages)
                    {
                        var nameBlobCrop = item.Key;
                        float[] cropCoordinates = item.Value;

                        if (nameBlobCrop == "Firma_Presidente_JAC" || nameBlobCrop == "ID_Presidente_JAC")
                        {
                            if (nameBlobCrop == "Firma_Presidente_JAC")
                            {
                                CropImage(imageBlobForm, nameBlobCrop, blobName, cropRectangleFirmaPresident);
                            }
                            else
                            {
                                CropImage(imageBlobForm, nameBlobCrop, blobName, cropRectangleIDPresident);
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

        /// <summary>
        /// Obtener CC de campo de cedula del formato de SANA POSESION
        /// </summary>
        /// <param name="blobName">Nombre de la imagen del formato</param>
        /// <returns></returns>
        private static async Task<string> CVIDAsync(string blobName)
        {
            //Console.WriteLine("\nIniciando la identificacion de firma del presidente...");
            // HANDWRITING

            string IDForm = "";
            string subscriptionKey = Environment.GetEnvironmentVariable("subscriptionKeyCV");

            try
            {
                string localFileNameID = Environment.GetEnvironmentVariable("localFilePath") + "ID_Presidente_JAC-" + blobName + ".png";
                string localFileNameFirma = Environment.GetEnvironmentVariable("localFilePath") + "Firma_Presidente_JAC-" + blobName + ".png";

                if (File.Exists(localFileNameID) && File.Exists(localFileNameFirma))
                {
                    ComputerVisionClient computerVision = new ComputerVisionClient(
                        new ApiKeyServiceClientCredentials(subscriptionKey),
                        new System.Net.Http.DelegatingHandler[] { });

                    computerVision.Endpoint = Environment.GetEnvironmentVariable("computerVisionEndpoint");
                    //Console.WriteLine("Imágenes siendo analizadas ...");

                    IDForm = await ExtractLocalTextAsync(computerVision, localFileNameID);
                    IDForm = string.Join("", IDForm.ToUpper().Split(" "));
                    IDForm = IDForm.Replace('S', '5');
                    IDForm = IDForm.Replace('I', '1');
                    IDForm = IDForm.Replace('G', '6');

                    return IDForm;
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
            int numberOfCharsInOperationId = 36;
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

        /// <summary>
        /// Recorte de Campos de imagen del formato de SANA POSESION
        /// </summary>
        /// <param name="imagecrop">Coordenadas de Recorte</param>
        /// <param name="nameCrop">Nombre de recorte</param>
        /// <param name="nameFile"></param>
        /// <param name="cropRect"></param>
        private static void CropImage(System.Drawing.Image imagecrop, string nameCrop, string nameFile, Rectangle cropRect)
        {
            string localFilename = Environment.GetEnvironmentVariable("localFilePath") + nameCrop + "-" + nameFile + ".png";

            Bitmap bmpImage = new Bitmap(imagecrop);
            bmpImage.Clone(cropRect, bmpImage.PixelFormat).Save(localFilename);
        }

        /// <summary>
        /// Prediccion de firma con cedula identificada en el formato de SANA POSESION
        /// </summary>
        /// <param name="respID"> Cedula identificada en el formato</param>
        /// <param name="numberBlob">Numero de formato</param>
        /// <param name="projectSignature">Proyecto de Custom Vision para verificacion de firma</param>
        /// <returns></returns>
        private static async Task<Dictionary<string, double>> CVSignatureAsync(string respID, string numberBlob, Project projectSignature)
        {
            Dictionary<string, double> logPredictionSignature = new Dictionary<string, double>();
            string localFileNameFirma = Environment.GetEnvironmentVariable("localFilePath") + "Firma_Presidente_JAC-" + numberBlob + ".png";
            // FIRMAS MODFIFICAR NOMBRE DE MODELO
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

        /// <summary>
        /// Borrar imagenes en cache 
        /// </summary>
        private static void EmptyFolderTemp()
        {
            string localFilename = Environment.GetEnvironmentVariable("localFilePath");

            System.IO.Directory.Delete(localFilename, true);
            System.IO.Directory.CreateDirectory(localFilename);
        }

        /// <summary>
        /// Obtener informacion de archivo .DAT con informacion de IDs
        /// </summary>
        /// <param name="url">Ubicacion del archivo .DAT</param>
        /// <returns></returns>
        private static string GetCSV(string url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();

            StreamReader sr = new StreamReader(resp.GetResponseStream());
            string results = sr.ReadToEnd();
            sr.Close();

            return results;
        }

        /// <summary>
        /// Construccion de archivo CSV y envio de Email a Analista de Fraude
        /// </summary>
        /// <returns></returns>
        private static async Task SendEmailCSV()
        {
            StringBuilder csvcontent = new StringBuilder();
            foreach (List<string> subList in InfoFilesImages.GeneralListPrint)
            {
                // Atributos a mostrar en el reporte
                string line = string.Join(",", subList);
                csvcontent.AppendLine(line);
            }

            using (MemoryStream stream = new MemoryStream(Encoding.ASCII.GetBytes(csvcontent.ToString())))
            {
                //MailMessage mail = new MailMessage("noreply@bancoagrario.gov.co", "kevinssanchez@outlook.com");
                MailMessage mail = new MailMessage("kevin.sanchez@mail.escuelaing.edu.co", "kevinssanchez@outlook.com");
                SmtpClient client = new SmtpClient();
                client.Port = 587;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.UseDefaultCredentials = false;
                client.EnableSsl = true;
                //client.Credentials = new NetworkCredential("noreply@bancoagrario.gov.co", "4grarioBAC2019*");
                client.Credentials = new NetworkCredential("kevin.sanchez@mail.escuelaing.edu.co", "52809327kessp");
                client.Host = "smtp.office365.com";

                //Add a new attachment to the E-mail message, using the correct MIME type
                DateTime dateForSearch = DateTime.Now.AddDays(-1);
                Attachment attachment = new Attachment(stream, new ContentType("text/csv"));
                attachment.Name = dateForSearch.ToString("yyyyMMdd") + "Report_Firmas.csv";
                mail.Attachments.Add(attachment);

                mail.Subject = "Reporte SANA POSESION - " + dateForSearch.ToString("yyyyMMdd");
                mail.Body = "Prueba";

                client.Send(mail);
            }
        }
    }
}
