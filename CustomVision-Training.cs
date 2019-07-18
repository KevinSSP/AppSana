using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Csv;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models;
using Microsoft.Azure.Storage.File;

namespace AppSANA
{
    public class Img
    {
        public Uri url = null;
        public string filepath = null;
        public List<Tag> tags = new List<Tag>();
    }

    public class Training
    {
        // Replace your training key here
        private readonly static string trainingKey = "604255a149f24640ac6fb1e62160c418";
        // Change the endpoint to your region if necessary
        private readonly static string endpoint = "https://eastus2.api.cognitive.microsoft.com";

        private static readonly CustomVisionTrainingClient trainingApi = new CustomVisionTrainingClient()
        {
            ApiKey = trainingKey,
            Endpoint = endpoint
        };

        public static async Task<Project> ProjectSetupPipeline(string projectName, string resourcesPath, string tagsDataset, bool create)
        {

            Project project = null;
            try
            {
                project = GetProject(projectName);
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n{e.GetType().Name}: Al fallar el proyecto Custom Vision.");
            }

            // If create is true, we create tags for the project
            if (create)
            {
                List<string> tags = null;
                try
                {
                    tags = GetTagsFromDataset(resourcesPath, tagsDataset);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"\n{e.GetType().Name}: No se pudieron obtener las etiquetas.");
                }

                if (project != null && tags != null)
                {
                    List<Tag> projectTags = null;
                    try
                    {
                        projectTags = await CreateOrGetTagsFromList(project, tags);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"\n{e.GetType().Name}: No se pudieron crear etiquetas para el proyecto.");
                    }

                    if (projectTags != null)
                    {

                    }
                }
            }

            return project;
        }

        public static Project GetProject(string projectName)
        {
            Console.WriteLine("\nObteniendo proyecto ...");
            Project project = null;

            try
            {
                IList<Project> projects = trainingApi.GetProjects();
                foreach (Project p in projects)
                {
                    if (p.Name == projectName)
                    {
                        project = trainingApi.GetProject(p.Id);
                        Console.WriteLine($"\nProyecto '{projectName}' fue encontrado.");
                        return project;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n{e.GetType().Name}: Error al obtener proyectos.");
                throw;
            }
            
            return project;
        } 

        public static Iteration TrainProject(Project project)
        {
            Console.WriteLine("\nEntrenando modelo...");
            Iteration iteration = null;
            try
            {
                iteration = trainingApi.TrainProject(project.Id);
                while (iteration.Status == "Training")
                {
                    Thread.Sleep(1000);
                    iteration = trainingApi.GetIteration(project.Id, iteration.Id);
                    Console.WriteLine($"\nEstado de iteración: {iteration.Status}");
                }
                Console.WriteLine($"\nEntrenado exitosamente el modelo Custom Vision.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n{e.GetType().Name}: No se pudo entrenar el modelo Custom Vision.");
            }

            return iteration;
        }

        public static void PublishProject(Project project, string modelName, Iteration iteration = null)
        {
            Console.WriteLine("\nPublicando...");

            if (iteration == null)
            {
                IList<Iteration> iterations = trainingApi.GetIterations(project.Id);
                iteration = iterations[iterations.Count - 1];
            }

            try
            {
                //trainingApi.PublishIteration(project.Id, iteration.Id, modelName, predictionResourceID);
                Console.WriteLine($"\nPublicado exitosamente modelo entrenado para el proyecto Custom Vision.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n{e.GetType().Name}: No se pudo publicar el modelo entrenado para el proyecto Custom Vision.");
            }
        }

        public static async Task<List<Tag>> CreateOrGetTagsFromList(Project project, List<string> tags)
        {
            List<Tag> projectTags = new List<Tag>();
            try
            {
                foreach (string t in tags)
                {
                        Tag tag = await CreateTag(project, t);
                        projectTags.Add(tag);
                }
                Console.WriteLine($"\n{projectTags.Count} Etiquetas creadas o recuperadas con éxito.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n{e.GetType().Name}: Error al crear etiquetas para el proyecto Custom Vision.");
                throw;
            }

            return projectTags;
        }

        public static async Task<Tag> CreateTag(Project project, string tag)
        {
            Tag projectTag = null;
            try
            {
                projectTag = await trainingApi.CreateTagAsync(project.Id, tag);         
                Console.WriteLine($"\nTag '{tag}' creado con éxito.");
            }
            catch
            {
                try
                {
                    projectTag = await GetTag(project, tag);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"\n{e.GetType().Name}: Error al crear la etiqueta para el proyecto Custom Vision.");
                    throw;
                }
            }

            return projectTag;
        }

        public static async Task<Tag> GetTag(Project project, string tag)
        {
            IList<Tag> projectTags = null;
            Tag projectTag = null;
            try
            {
                projectTags = await trainingApi.GetTagsAsync(project.Id);
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n{e.GetType().Name}: Error al recuperar la lista de etiquetas del proyecto Custom Vision.");
                throw;
            }

            if (projectTags != null)
            {
                foreach(Tag t in projectTags)
                {
                    if(t.Name == tag)
                    {
                        try
                        {
                            projectTag = await trainingApi.GetTagAsync(project.Id, t.Id);
                            return projectTag;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"\n{e.GetType().Name}: Error al recuperar la etiqueta '{tag}' del proyecto Custom Vision.");
                            throw;
                        }
                    }
                }
                Console.WriteLine($"\nNo tag '{tag}' Fue encontrado en el proyecto Custom Vision.");
            }

            return projectTag;
        }

        public static async Task UploadAndTagImages(Project project,List<Img> images, string mode = "url")
        {
            if (mode == "url")
            {
                var imageUrls = new List<ImageUrlCreateEntry>();
                foreach (Img img in images)
                {
                    imageUrls.Add(GetImageUrlEntry(img.url.ToString(), img.tags));
                }
                try
                {
                    int total = imageUrls.Count;
                    int batches = (int)Math.Floor(total / 64f) + 1;
                    for (int i = 0; i < batches - 1; i++)
                    {
                        int indexStart = i * 64;
                        var currentBatch = imageUrls.GetRange(indexStart, 64);
                        await trainingApi.CreateImagesFromUrlsAsync(project.Id, new ImageUrlCreateBatch(currentBatch));
                        Console.WriteLine($"\nImágenes cargadas exitosamente de urls en batch.");
                    }
                    if ((batches - 1) * 64 < total)
                    {
                        int count = total - (batches - 1) * 64;
                        var currentBatch = imageUrls.GetRange((batches - 1) * 64, count);
                        await trainingApi.CreateImagesFromUrlsAsync(project.Id, new ImageUrlCreateBatch(currentBatch));
                        Console.WriteLine($"\nImágenes cargadas exitosamente de urls en batch.");
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine($"\n{e.GetType().Name}: No se pudieron cargar imágenes en lote de urls.");
                }
            }
            else
            {
                var imageFiles = new List<ImageFileCreateEntry>();
                foreach (Img img in images)
                {
                    imageFiles.Add(GetImageFileEntry(img.filepath, img.tags));
                }
                try
                {
                    
                    int total = imageFiles.Count;
                    int batches = (int)Math.Floor(total / 64f) + 1;
                    for(int i=0; i < batches - 1; i++)
                    {
                        int indexStart = i * 64;
                        var currentBatch = imageFiles.GetRange(indexStart, 64);
                        await trainingApi.CreateImagesFromFilesAsync(project.Id, new ImageFileCreateBatch(currentBatch));
                        Console.WriteLine($"\nImágenes cargadas con éxito desde archivos en lote.");
                    }
                    if((batches-1)*64 < total)
                    {
                        int count = total - (batches-1) * 64;
                        var currentBatch = imageFiles.GetRange((batches-1)*64, count);
                        await trainingApi.CreateImagesFromFilesAsync(project.Id, new ImageFileCreateBatch(currentBatch));
                        Console.WriteLine($"\nImágenes cargadas con éxito desde archivos en lote.");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"\n{e.GetType().Name}: No se pudieron cargar imágenes en lote de archivos.");
                }
            }
        }

        public static ImageUrlCreateEntry GetImageUrlEntry(string url, List<Tag> tags)
        {
            List<Guid> tagsIDs = new List<Guid>();
            ImageUrlCreateEntry imageUrl;

            foreach (Tag t in tags)
            {
                tagsIDs.Add(t.Id);
            }

            try
            {
                imageUrl = new ImageUrlCreateEntry(url, tagsIDs);
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n{e.GetType().Name}: No se pudo crear la entrada de la imagen url para la imagen '{url}'.");
                throw;
            }

            return imageUrl;
        }

        public static ImageFileCreateEntry GetImageFileEntry(string filepath, List<Tag> tags)
        {
            List<Guid> tagsIDs = new List<Guid>();
            ImageFileCreateEntry imageFile = null;

            foreach (Tag t in tags)
            {
                tagsIDs.Add(t.Id);
            }

            try
            {
                Console.WriteLine($"\nCrear entrada de imagen para archivo '{Path.GetFileName(filepath)}' situado en '{filepath}'.");
                imageFile = new ImageFileCreateEntry(Path.GetFileName(filepath), File.ReadAllBytes(filepath), tagsIDs);
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n{e.GetType().Name}: No se pudo crear la entrada del archivo de imagen para la imagen '{filepath}'.");
            }

            return imageFile;
        }

        private static List<string> GetTagsFromDataset(string resourcesPath, string tagsDataset)
        {
            List<string> tags = new List<string>();

            IEnumerable<ICsvLine> dataset = null;

            try
            {
                string path = Path.GetFullPath(resourcesPath+tagsDataset);
                Console.WriteLine($"\nTags dataset: '{path}'");
                //dataset = LoadDataset(path);
            }
            catch (Exception e)
            {
                Console.WriteLine($"\n{e.GetType().Name}: Error al cargar las etiquetas del conjunto de datos.");
                throw;
            }

            if (dataset != null)
            {
                try
                {
                    foreach (var line in dataset)
                    {
                        tags.Add(line[0]);
                    }
                    Console.WriteLine($"\nTags list created: {tags.Count} tags found.");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"\n{e.GetType().Name}: Error al obtener las etiquetas.");
                    throw;
                }

            }
            
            return tags;
        }
    }
}
