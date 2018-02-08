namespace HomeSafeAgentCore
{
    using System;
    using System.Net;
    using System.Threading;
    using System.IO;
    using System.Text;
    using System.Drawing;
    using System.Drawing.Imaging;

    /// <summary>
    /// Represents a home safe IP camera device.
    /// </summary>
    public class Camera
    {
        const int BUFFER_SIZE = 512 * 1024;
        const int BYTES_PER_READ = 1024;

        // 
        private const string url = "http://{0}/videostream.cgi?user={1}&pwd={2}";
        private Thread cameraThread, checkForMovementThread;
        HttpWebRequest request;
        WebResponse response;
        Stream stream;

        int framesReceivedCurrentStream;
        int bytesReceivedCurrentStream;

        byte[] buffer;
        byte[] boundary, delimiter1, delimiter2;
        int boundaryLength, delimiter1Length, delimiter2Length;
        int align;
        int start, stop;

        int totalBytesRead, boundaryPosition, bytesAfterBoundary;

        string contentType;

        // motion detection variables
        Image currentFrame;
        Image previousFrame;
        bool skipFrame, skipFrame2, skipFrame3, skipFrame4;
        bool threadStart;

        /// <summary>
        /// Constructor: formats the class property StreamUrl to specify the camera it is working with
        /// </summary>
        /// <param name="cameraIP"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public Camera(string cameraIP, string username, string password)
        {
            StreamUrl = String.Format(url, cameraIP, username, password);
        }

        /// <summary>
        /// 
        /// </summary>
        public ICameraDataReceiver CameraDataReceiver { get; set; }

        /// <summary>
        /// Run camera automation service thread
        /// </summary>
        public void StartCameraThread()
        {
            cameraThread = new Thread(new ThreadStart(cameraAutomationStream));
            cameraThread.Start();
        }

        /// <summary>
        /// Stop camera automation service thread
        /// </summary>
        public void StopCameraThread()
        {
            cameraThread.Abort();
            cameraThread = null;
        }

        /// <summary>
        /// Class property: contains url of camera and the command to receive stream from camera
        /// </summary>
        public string StreamUrl { get; private set; }

        /// <summary>
        /// Method that receives and manages the camera's stream
        /// </summary>
        private void cameraAutomationStream()
        {
            buffer = new byte[BUFFER_SIZE];
            bytesReceivedCurrentStream = 0;
            framesReceivedCurrentStream = 0;

            while (true)
            {
                request = null;
                response = null;
                stream = null;

                boundary = null; delimiter1 = null; delimiter2 = null;
                delimiter1Length = 0; delimiter2Length = 0;
                align = 1;
                start = 0; stop = 0;

                totalBytesRead = 0; boundaryPosition = 0; bytesAfterBoundary = 0;

                currentFrame = null; previousFrame = null;
                skipFrame = false;
                skipFrame2 = false;
                skipFrame3 = false;
                skipFrame4 = false;
                threadStart = false;

                try
                {
                    request = (HttpWebRequest)WebRequest.Create(StreamUrl);
                    response = request.GetResponse();
                    stream = response.GetResponseStream();

                    // get data boundary
                    contentType = response.ContentType;
                    FindDataBoundary();

                    while (true)
                    {
                        // refresh variables when buffer's unread data is too small
                        if (totalBytesRead + BYTES_PER_READ > BUFFER_SIZE)
                        {
                            totalBytesRead = boundaryPosition = bytesAfterBoundary = 0;
                        }

                        // load data into buffer
                        /// reads 1024 bytes into buffer starting from the totalBytesRead index
                        int read = stream.Read(buffer, totalBytesRead, BYTES_PER_READ);
                        if (read == 0) // read is 0 when end of stream has been reached
                            throw new ApplicationException();

                        totalBytesRead += read;
                        bytesAfterBoundary += read;

                        bytesReceivedCurrentStream += read;

                        FindDelimiterBoundary();

                    }
                }
                catch { }
                finally
                {
                    if (request != null)
                    {
                        request.Abort();
                        request = null;
                    }
                    if (stream != null)
                    {
                        stream.Close();
                        stream = null;
                    }
                    if (response != null)
                    {
                        response.Close();
                        response = null;
                    }
                }
            }
        }

        private void FindDelimiterBoundary()
        {
            if (delimiter1 == null)
            {
                boundaryPosition = ByteArrayUtils.Find(buffer, boundary, boundaryPosition, bytesAfterBoundary);

                if (boundaryPosition == -1)
                {
                    // was not found
                    bytesAfterBoundary = boundaryLength - 1;
                    boundaryPosition = totalBytesRead - bytesAfterBoundary;
                    return;
                }

                bytesAfterBoundary = totalBytesRead - boundaryPosition;

                if (bytesAfterBoundary < 2)
                {
                    return;
                }

                // delimiter position is right after the boundary on boundaryPosition
                // find what type of delimiter is present
                if (buffer[boundaryPosition + boundaryLength] == 10)
                {
                    delimiter1Length = 2;
                    delimiter1 = new byte[2] { 10, 10 };
                    delimiter2Length = 1;
                    delimiter2 = new byte[1] { 10 };
                }
                else
                {
                    delimiter1Length = 4;
                    delimiter1 = new byte[4] { 13, 10, 13, 10 };
                    delimiter2Length = 2;
                    delimiter2 = new byte[2] { 13, 10 };
                }

                boundaryPosition += boundaryLength + delimiter2Length;
                bytesAfterBoundary = totalBytesRead - boundaryPosition;
            }

            SearchForImage();
        }

        private void SearchForImage()
        {
            if (align == 1)
            {
                start = ByteArrayUtils.Find(buffer, delimiter1, boundaryPosition, bytesAfterBoundary);
                if (start != -1)
                {
                    // found delimiter
                    start += delimiter1Length;
                    boundaryPosition = start; // boundary position is now the index of the first image data
                    bytesAfterBoundary = totalBytesRead - boundaryPosition; // todo is now the amount of data from the first image to the end
                    align = 2;
                }
                else
                {
                    // delimiter not found
                    bytesAfterBoundary = delimiter1Length - 1;
                    boundaryPosition = totalBytesRead - bytesAfterBoundary;
                }
            }

            // search for image end
            while ((align == 2) && (bytesAfterBoundary >= boundaryLength))
            {
                stop = ByteArrayUtils.Find(buffer, boundary, boundaryPosition, bytesAfterBoundary); /// find boundary after image
                if (stop != -1)
                {
                    boundaryPosition = stop;
                    bytesAfterBoundary = totalBytesRead - boundaryPosition;

                    // increment frames counter
                    framesReceivedCurrentStream++;

                    // retrieve image
                    CameraDataReceiver.OnCameraDataReady((Bitmap)Bitmap.FromStream(new MemoryStream(buffer, start, stop - start)));
                    previousFrame = currentFrame;
                    currentFrame = (Bitmap)Bitmap.FromStream(new MemoryStream(buffer, start, stop - start));
                    // do this every other frame
                    if (!skipFrame)
                    {
                        if (!threadStart)
                        {
                            threadStart = true;
                            checkForMovementThread = new Thread(new ThreadStart(CheckForMovement));
                            checkForMovementThread.Start();
                        }
                        else if (!checkForMovementThread.IsAlive)
                        {
                            checkForMovementThread = new Thread(new ThreadStart(CheckForMovement));
                            checkForMovementThread.Start();
                            skipFrame = true;
                        }
                    }
                    //else if (!skipFrame2)
                    //{
                    //    skipFrame2 = true;
                    //}
                    else
                    {
                        skipFrame = false;
                        skipFrame2 = false;
                    }

                    // shift array
                    boundaryPosition = stop + boundaryLength;
                    bytesAfterBoundary = totalBytesRead - boundaryPosition; ///todo is now the amount of data after the boundary following the image
                    Array.Copy(buffer, boundaryPosition, buffer, 0, bytesAfterBoundary); ///copy the todo into the beginning of buffer

                    totalBytesRead = bytesAfterBoundary;
                    boundaryPosition = 0;
                    align = 1;
                }
                else
                {
                    // delimiter not found
                    bytesAfterBoundary = boundaryLength - 1;
                    boundaryPosition = totalBytesRead - bytesAfterBoundary;
                }
            }
        }

        private void FindDataBoundary()
        {

            if (contentType.IndexOf("multipart/x-mixed-replace") == -1) // check if content type is valid
            {
                throw new ApplicationException("Invalid URL");
            }

            // get the boundary value in contentType
            ASCIIEncoding encoding = new ASCIIEncoding();
            boundary = encoding.GetBytes(contentType.Substring(contentType.IndexOf("boundary=", 0) + 9));
            boundaryLength = boundary.Length;
        }

        void CheckForMovement()
        {
            try
            {
                if (previousFrame != null)
                {
                    Bitmap previousFrameBm = new Bitmap(previousFrame);
                    Bitmap currentFrameBm = new Bitmap(currentFrame);

                    previousFrameBm.SetResolution(previousFrameBm.Width, previousFrame.Height);
                    currentFrameBm.SetResolution(currentFrame.Width, currentFrame.Height);

                    // Figure out if camera is moving laterally or vertically
                    // solution: if almost every pixel in frame is changed, then we know that it's because of camera movement and not thief

                    int previousR, previousG, previousB;
                    int currentR, currentG, currentB;

                    int pixelSameCount = 0;

                    for (int x = 0; x < previousFrameBm.Width; x+=3)
                    {
                        for (int y = 0; y < previousFrameBm.Height; y+=3)
                        {
                            previousR = previousFrameBm.GetPixel(x, y).R;
                            previousB = previousFrameBm.GetPixel(x, y).B;
                            previousG = previousFrameBm.GetPixel(x, y).G;
                            currentR = currentFrameBm.GetPixel(x, y).R;
                            currentB = currentFrameBm.GetPixel(x, y).B;
                            currentG = currentFrameBm.GetPixel(x, y).G;

                            // check if pixel color changes drastically between previous frame and current frame

                            // if color is different
                            if (previousR < currentR - 4 || previousR > currentR + 4)
                            {
                                // color changed
                                continue;
                            }
                            if (previousB < currentB - 4 && previousB > currentB + 4)
                            {
                                // color changed
                                continue;
                            }
                            if (previousG < currentG - 4 && previousG > currentG + 4)
                            {
                                // color changed
                                continue;
                            }

                            // pixel color is drastically different
                            pixelSameCount++;
                        }
                    }

                    // check result of frame comparison
                    double pixelSameCountDec = double.Parse(pixelSameCount.ToString());
                    double totalPixels = (previousFrameBm.Width/3+1) * (previousFrameBm.Height/3+1);
                    double percentDifferent = (totalPixels - pixelSameCountDec) / totalPixels;
                    if (percentDifferent > 0.60) // frames have less than 90% similarity
                    {
                        // intruder alert
                        Console.WriteLine("Frame Moved, no alert");
                        Console.WriteLine(percentDifferent);
                    }
                    else
                    {
                        Console.WriteLine("Nothing happening" + previousFrameBm);
                        Console.WriteLine(percentDifferent);
                    }
                }
            }
            catch { }
        }

    }
}
