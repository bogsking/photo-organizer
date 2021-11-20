using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;

namespace PhotoOrganizer.Worker
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine($"Photo organizer");
                Console.WriteLine();

                if (args == null ||
                    args.Length < 2)
                    throw new ArgumentNullException(nameof(args), "Destination and source folder must be specified");

                var destinationFolder = args[0];
                if (!Directory.Exists(destinationFolder))
                    Directory.CreateDirectory(destinationFolder);
                destinationFolder = (new DirectoryInfo(destinationFolder)).FullName;

                var unprocessedFolder = args[1];
                if (!Directory.Exists(unprocessedFolder))
                    Directory.CreateDirectory(unprocessedFolder);
                unprocessedFolder = (new DirectoryInfo(unprocessedFolder)).FullName;

                var sourceFolder = args[2];
                if (!Directory.Exists(sourceFolder))
                    throw new ArgumentNullException(nameof(sourceFolder), $"{sourceFolder} does not exist!");
                sourceFolder = (new DirectoryInfo(sourceFolder)).FullName;

                await ProcessDirectory(destinationFolder, unprocessedFolder, sourceFolder);
            }
            catch (Exception generalError)
            {
                Console.WriteLine(generalError);
            }

            Console.WriteLine("Done!");
            Console.Read();
        }

        private static async Task ProcessDirectory(string destinationFolder, string unprocessedFolder, string sourceFolder)
        {
            var files = Directory.GetFiles(sourceFolder);
            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    Console.WriteLine($"Processing {fileInfo.Name}...       ");


                    var name = fileInfo.Name.Replace(".00", "");
                    var dotIndex = name.LastIndexOf('.');
                    if (dotIndex < 0)
                        throw new Exception("Failed to find dot!");
                    var extension = name.Substring(dotIndex).ToLower();

                    switch (extension)
                    {
                        default:
                            await UnprocessFile(unprocessedFolder, file);
                            break;

                        case ".jpg":
                        case ".mov":
                        case ".mp4":
                        case ".png":
                        case ".jpeg":
                            {
                                var category = "";
                                var fileShell = ShellFile.FromFilePath(file);
                                IShellProperty dateShellPropery = null;

                                switch (extension)
                                {
                                    default:
                                    case ".jpg":
                                    case ".png":
                                    case ".jpeg":
                                        {
                                            dateShellPropery = fileShell.Properties.GetProperty(SystemProperties.System.Photo.DateTaken);
                                            category = "Photos";
                                        }
                                        break;

                                    case ".mov":
                                    case ".mp4":
                                        {
                                            dateShellPropery = fileShell.Properties.GetProperty(SystemProperties.System.Media.DateEncoded);
                                            category = "Videos";
                                        }
                                        break;
                                }

                                var dateCreated = DateTime.Now;
                                if (dateShellPropery != null &&
                                    dateShellPropery.ValueAsObject is DateTime)
                                {
                                    dateCreated = (DateTime)dateShellPropery.ValueAsObject;
                                }

                                var folderMonth = $"{destinationFolder}\\{category}\\{dateCreated:yyyy.MM MMMM}";
                                if (!Directory.Exists(folderMonth))
                                    Directory.CreateDirectory(folderMonth);
                                folderMonth = (new DirectoryInfo(folderMonth)).FullName;

                                var fileDesitnation = $"{folderMonth}\\{dateCreated:yyyy.MM.dd HH.mm.ss.fff MMMM dddd}{extension}";

                                await MoveFile(file, fileDesitnation);
                            }
                            break;
                    }
                }
                catch (Exception generalError)
                {
                    Console.WriteLine(generalError);
                    Console.WriteLine();
                    Console.ReadKey();

                    Console.WriteLine();

                    await UnprocessFile(unprocessedFolder, file);
                }

                Thread.Sleep(10);
            }

            var folders = Directory.GetDirectories(sourceFolder);
            foreach (var folder in folders)
            {
                await ProcessDirectory(destinationFolder, unprocessedFolder, folder);
            }
        }

        private static async Task UnprocessFile(string unprocessedFolder, string file)
        {
            var fileInfo = new FileInfo(file);
            var fileDestination = $"{unprocessedFolder}\\{fileInfo.Name}";

            await MoveFile(file, fileDestination);
        }

        private static async Task MoveFile(string file, string fileDestination)
        {
            var proceed = true;

            if (File.Exists(fileDestination))
            {
                if (!(await IsFileSame(file, fileDestination)))
                {
                    var fileInfoDestination = new FileInfo(fileDestination);
                    fileDestination = $"{fileInfoDestination.DirectoryName}\\{fileInfoDestination.Name.Replace(fileInfoDestination.Extension, "")}.00{fileInfoDestination.Extension}";

                    await MoveFile(file, fileDestination);

                    return;
                }
                else
                {
                    proceed = false;

                    File.Delete(file);
                }
            }

            if (proceed)
                File.Move(file, fileDestination);
        }

        private static async Task<bool> IsFileSame(string fileA, string fileB)
        {
            var sha = SHA256.Create();

            var bufferA = File.ReadAllBytes(fileA);
            var hashA = sha.ComputeHash(bufferA);
            var hashAString = Convert.ToBase64String(hashA);

            var bufferB = File.ReadAllBytes(fileB);
            var hashB = sha.ComputeHash(bufferB);
            var hashBString = Convert.ToBase64String(hashB);

            await Task.CompletedTask;

            return hashAString == hashBString;
        }
    }
}