using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MoreLinq;
using System.IO;
using System.Threading;

namespace SPP1.ExternalMergeSort
{
    public class ExternalMergeSorter
    {
        public enum SortStatus { InProgress, Completed }

        public class ProgressMessagePostedEventArgs : EventArgs
        {
            public string Message { get; private set; }
            public SortStatus Status { get; private set; }

            public ProgressMessagePostedEventArgs(string message, SortStatus status)
            {
                this.Message = message;
                this.Status = status;
            }
        }

        public event EventHandler<ProgressMessagePostedEventArgs> ProgressMessagePosted;

        private CancellationToken cancellationToken;

        private string fileName;
        private int chunkSize;
        private bool separateOutputFile;
        private List<string> tempFiles;

        public ExternalMergeSorter(string fileName, int chunkSize, bool separateOutputFile, CancellationToken cancellationToken)
        {
            if (chunkSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be a positive integer.");

            this.cancellationToken = cancellationToken;
            this.fileName = fileName;
            this.chunkSize = chunkSize;
            this.separateOutputFile = separateOutputFile;
            this.tempFiles = new List<string>();
        }

        public async Task Sort()
        {
            if (!File.Exists(fileName))
                throw new FileNotFoundException("File doesn't exist.", fileName);

            var availableDiskSpace = DriveInfo.GetDrives().Where((DriveInfo driveInfo) =>
            {
                return driveInfo.RootDirectory.FullName == Path.GetPathRoot(Environment.CurrentDirectory) ? true : false;
            }).Single().TotalFreeSpace;
            if (availableDiskSpace < (separateOutputFile ? 2 : 1) * (new FileInfo(fileName)).Length)
                throw new Exception("Insufficient disk space.");

            var startTime = DateTime.Now;

            PostProgressMessage("Sorting " + fileName + " ...", SortStatus.InProgress);

            //step 1: divide file into chunks and sort them
            await SortChunks();

            //step 2: merge chunks into 1 file
            MergeChunks();

            foreach (var file in tempFiles)
                File.Delete(file);
            PostProgressMessage("Sort completed in " + (DateTime.Now - startTime).ToString(), SortStatus.Completed);
        }

        private void MergeChunks()
        {
            PostProgressMessage("Merging chunks into output file...", SortStatus.InProgress);

            //for each chunk creates a key-value pair of 1st line and enumerator, and adds the pair to the queue
            var priorityQueue = new SortedList<string, IEnumerator<string>>();
            foreach (var file in tempFiles)
            {
                var chunkEnumerator = File.ReadLines(file).GetEnumerator();
                chunkEnumerator.MoveNext();
                var line = chunkEnumerator.Current;
                chunkEnumerator.MoveNext();
                priorityQueue.Add(line, chunkEnumerator);
            }

            var outputStream = File.Open(separateOutputFile ? AppendToFileName(fileName, " sorted") : fileName, FileMode.Create);
            var outputWriter = new StreamWriter(outputStream, Encoding.Unicode);

            //takes the first element of the queue and writes it to the file, adds an element with the next line of the corresponding enumerator
            string nextLine;
            KeyValuePair<string, IEnumerator<string>> nextElem;
            int linesWritten = 0;
            while (priorityQueue.Any())
            {
                nextElem = priorityQueue.First();
                priorityQueue.RemoveAt(0);
                outputWriter.WriteLine(nextElem.Key);
                linesWritten += 1;
                if (nextElem.Value != null)
                {
                    nextLine = nextElem.Value.Current;
                    //if the enumerator is empty, only its last line is added to the queue, with null representing an empty enumerator
                    priorityQueue.Add(nextLine, nextElem.Value.MoveNext() ? nextElem.Value : null);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    foreach (var item in priorityQueue)
                    {
                        item.Value.Dispose();
                    }
                    outputWriter.Close();

                    foreach (var file in tempFiles)
                        File.Delete(file);
                    
                    if (separateOutputFile)
                        PostProgressMessage("Task was cancelled.", SortStatus.Completed);
                    else
                        PostProgressMessage("Task was cancelled. Warning: input file has been partly overwritten.", SortStatus.Completed);

                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            outputWriter.Flush();
            outputStream.Flush();
            outputWriter.Close();

            PostProgressMessage("Merge complete.", SortStatus.InProgress);
        }

        private async Task SortChunks()
        {
            PostProgressMessage("Sorting lines in chunks of " + chunkSize.ToString() + "...", SortStatus.InProgress);

            var chunks = File.ReadLines(fileName).Batch(chunkSize);

            Task asyncWriteTask = null;

            int chunksCounter = 0;
            foreach (var chunkEnumerator in chunks)
            {
                PostProgressMessage("Sorting chunk #" + (++chunksCounter).ToString() + "...", SortStatus.InProgress);

                var tempFileName = Path.GetTempFileName();
                tempFiles.Add(tempFileName);

                var chunk = chunkEnumerator.ToList();
                chunk.Sort((string x, string y) =>
                {
                    return string.Compare(x, y, StringComparison.Ordinal);
                });

                PostProgressMessage("Finished sorting chunk #" + chunksCounter.ToString() + ", writing to file...", SortStatus.InProgress);

                if (asyncWriteTask != null)
                    await asyncWriteTask;

                if (cancellationToken.IsCancellationRequested)
                {
                    foreach (var file in tempFiles)
                        File.Delete(file);

                    PostProgressMessage("Task was cancelled.", SortStatus.Completed);

                    cancellationToken.ThrowIfCancellationRequested();
                }

                var outputStream = new FileStream(tempFileName, FileMode.Create);
                var outputWriter = new StreamWriter(outputStream, Encoding.Unicode);
                outputWriter.AutoFlush = true;

                asyncWriteTask = new Task(() => WriteLines(outputWriter, chunk));
                asyncWriteTask.Start();
            }

            if (asyncWriteTask != null)
                await asyncWriteTask;

            PostProgressMessage("Finished sorting individual chunks.", SortStatus.InProgress);
        }

        private static void WriteLines(StreamWriter writer, List<string> lines)
        {
            foreach (var line in lines)
                writer.WriteLine(line);

            writer.Flush();
            writer.Close();
        }

        private static string AppendToFileName(string path, string appendix)
        {
            return Path.GetDirectoryName(path) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(path) + appendix + Path.GetExtension(path);
        }

        private void PostProgressMessage(string message, SortStatus status)
        {
            if (ProgressMessagePosted != null)
                ProgressMessagePosted(this, new ProgressMessagePostedEventArgs(message, status));
        }

        //mad ting
        public void AtLeastMyMemoryIsFreeSort()
        {
            var inputLinesCount = File.ReadLines(fileName).Count();
            int linesWritten = 0;

            var reader = new StreamReader(fileName, Encoding.Unicode);
            var writer = new StreamWriter(AppendToFileName(fileName, " kek"), false, Encoding.Unicode);

            String currentLine, newLine;
            currentLine = reader.ReadLine();
            while (!reader.EndOfStream)
            {
                newLine = reader.ReadLine();
                if (String.Compare(newLine, currentLine, StringComparison.Ordinal) < 0)
                    currentLine = newLine;
            }
            reader.BaseStream.Seek(0, SeekOrigin.Begin);

            writer.WriteLine(currentLine);
            linesWritten++;
            PostProgressMessage("Written " + linesWritten.ToString() + " lines", SortStatus.InProgress);

            String previousLine = currentLine;
            while (linesWritten < inputLinesCount)
            {
                currentLine = reader.ReadLine();
                while (String.Compare(currentLine, previousLine, StringComparison.Ordinal) <= 0 && !reader.EndOfStream)
                {
                    currentLine = reader.ReadLine();
                }

                while (!reader.EndOfStream)
                {
                    newLine = reader.ReadLine();
                    if (String.Compare(newLine, currentLine, StringComparison.Ordinal) < 0 && String.Compare(newLine, previousLine, StringComparison.Ordinal) > 0)
                        currentLine = newLine;
                }

                writer.WriteLine(currentLine);
                linesWritten++;
                reader.BaseStream.Seek(0, SeekOrigin.Begin);

                PostProgressMessage("Written " + linesWritten.ToString() + " lines", SortStatus.InProgress);
            }

            reader.Close();
            writer.Close();

            PostProgressMessage("Done", SortStatus.Completed);
        }

        public static int EstimateChunkSize(string filePath)
        {
            int sampleSize = 0, sampleLength = 100;
            var lineSizes = File.ReadLines(filePath, Encoding.Unicode).Take(sampleLength).Select(line => line.Length * 2);
            foreach (var lineSize in lineSizes)
                sampleSize += lineSize;
            var fileSize = new FileInfo(filePath).Length;
            double fileSizeGb = fileSize / (double)(1073741824L);
            int suggestedNumberOfChunks = (int)Math.Ceiling(fileSizeGb * 10); //10 chunks for every GB
            var suggestedChunkSize = fileSize / (sampleSize / sampleLength) / suggestedNumberOfChunks;

            return (int)suggestedChunkSize;
        }
    }
}
