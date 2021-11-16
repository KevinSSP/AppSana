using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AppSANA
{
    public class Prediction
    {
        // Replace your prediction key here
        private readonly static string predictionKey = "415443b344a04439988564a0d668b0c3";
        // Change the endpoint to your region if necessary
        private readonly static string predictionEndpoint = "https://eastus2.api.cognitive.microsoft.com";

        private static readonly CustomVisionPredictionClient endpoint = new CustomVisionPredictionClient()
        {
            ApiKey = predictionKey,
            Endpoint = predictionEndpoint
        };

        public static async Task<ImagePrediction> PredictImageURLSignature(Guid projectID, string modelName, string url)
        {
            ImageUrl imageUrl = new ImageUrl(url);

            ImagePrediction result = null;

            try
            {
                result = await endpoint.ClassifyImageUrlAsync(projectID, modelName, imageUrl);
                //Console.WriteLine($"\nRecuperado con éxito las predicciones para la imagen. '{url}'.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n{e.GetType().Name}: {e.Message} \nNo se pudo obtener la predicción de la imagen. '{url}'.");
            }

            // Loop over each prediction and write out the results
            if (result != null)
            {
                foreach (var c in result.Predictions)
                {
                    Console.WriteLine($"\t{c.TagName}: {c.Probability:P1}");
                }
            }

            return result;
        }

        public static async Task<Dictionary<string, float[]>> PredictImageURLForm(Guid projectID, string modelName, string url)
        {   
            ImageUrl imageUrl = new ImageUrl(url);
            ImagePrediction result = null;
            Dictionary<string, float[]> logImages = new Dictionary<string, float[]>();

            try
            {
                result = await endpoint.DetectImageUrlAsync(projectID, modelName, imageUrl);
                //Console.WriteLine($"\nRecuperado con éxito las predicciones para la imagen. '{url}'.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n{e.GetType().Name}: {e.Message} \nNo se pudo obtener la predicción de la imagen. '{url}'.");
            }

            // Loop over each prediction and write out the results
            if (result != null)
            {
                foreach (var c in result.Predictions)
                {
                    if (c.Probability > 0.20 && !logImages.ContainsKey(c.TagName))
                    {
                        logImages.Add(c.TagName, new float[] { (float)c.BoundingBox.Left, (float)c.BoundingBox.Top, (float)c.BoundingBox.Width, (float)c.BoundingBox.Height });
                        Console.WriteLine($"\t{c.TagName}: {c.Probability:P1}");
                    }
                }
            }

            return logImages;
        }

        public static async Task<Dictionary<string, double>> PredictImageFile(Guid projectID, string modelName, string file, string idPresident)
        {
            var img = new MemoryStream(File.ReadAllBytes(file));

            ImagePrediction result = null;

            Dictionary<string, double> logImages = new Dictionary<string, double>();

            try
            {
                result = await endpoint.ClassifyImageAsync(projectID, modelName, img);
                //Console.WriteLine($"\nRecuperado con éxito las predicciones para la imagen. '{file}'.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n{e.GetType().Name}: {e.Message} \nNo se pudo obtener la predicción de la imagen. '{file}'.");
            }

            // Loop over each prediction and write out the results
            if (result != null)
            {
                foreach (var c in result.Predictions)
                {
                    if (c.TagName.Split("-")[0] == idPresident)
                    {
                        Console.WriteLine($"\t{c.TagName}: {c.Probability:P1}");
                        logImages.Add(c.TagName, c.Probability);
                    }
                }
            }

            return logImages;
        }

    }
}
