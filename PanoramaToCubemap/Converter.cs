﻿/* (C) 2018 - Premysl Fara */

namespace PanoramaToCubemap
{
    using System;


    /// <summary>
    /// A 3D vector.
    /// </summary>
    public class Vector3
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }


        public Vector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }


    /// <summary>
    /// Converts panoramatic 2:1 image with a equirectangular projection to a 6 cube maps.
    /// </summary>
    public class Converter
    {
        public const string PositiveZOrientationFaceName = "pz";
        public const string NegativeZOrientationFaceName = "nz";
        public const string PositiveXOrientationFaceName = "px";
        public const string NegativeXOrientationFaceName = "nx";
        public const string PositiveYOrientationFaceName = "py";
        public const string NegativeYOrientationFaceName = "ny";


        /// <summary>
        /// Renders a cube face from a 2:1 panoramatic image.
        /// </summary>
        /// <param name="readData">Source image data.</param>
        /// <param name="faceName">A name of a face to be rendered.</param>
        /// <param name="rotation">A cube rotation. 0 .. 359</param>
        /// <param name="interpolation">Which image interpolation should be used.</param>
        /// <param name="maxWidth">The maximum width of the generated image.</param>
        /// <returns>A image representing a cube face.</returns>
        public ImageData RenderFace(ImageData readData, string faceName, double rotation, string interpolation, int maxWidth = int.MaxValue)
        {
            var faceWidth = Math.Min(maxWidth, readData.Width / 4);
            var faceHeight = faceWidth;

            var writeData = new ImageData(faceWidth, faceHeight, 4);
            var cubeOrientation = GetOrientation(faceName);
            var copyPixel = GetCopyPixelDelegate(interpolation);

            for (var x = 0; x < faceWidth; x++)
            {
                for (var y = 0; y < faceHeight; y++)
                {
                    var to = 4 * (y * faceWidth + x);

                    // fill alpha channel
                    writeData.Data[to + 3] = 255;

                    // get position on cube face
                    // cube is centered at the origin with a side length of 2
                    var cube = cubeOrientation(2 * (x + 0.5) / faceWidth - 1, 2 * (y + 0.5) / faceHeight - 1);

                    // project cube face onto unit sphere by converting cartesian to spherical coordinates
                    var r = Math.Sqrt(cube.X * cube.X + cube.Y * cube.Y + cube.Z * cube.Z);
                    var lon = Mod(Math.Atan2(cube.Y, cube.X) + rotation, 2.0 * Math.PI);
                    var lat = Math.Acos(cube.Z / r);

                    copyPixel(readData, writeData, readData.Width * lon / Math.PI / 2 - 0.5, readData.Height * lat / Math.PI - 0.5, to);
                }
            }

            return writeData;
        }


        private int Clamp(int x, int min, int max)
        {
            return Math.Min(max, Math.Max(x, min));
        }


        private double Mod(double x, double n)
        {
            return ((x % n) + n) % n;
        }


        private int GetReadIndex(int x, int y, int width)
        {
            return 4 * (y * width + x);
        }


        private delegate void CopyPixelDelegate(ImageData read, ImageData write, double xFrom, double yFrom, int to);


        private CopyPixelDelegate GetCopyPixelDelegate(string interpolation)
        {
            //const copyPixel =
            //  interpolation === 'linear' ? copyPixelBilinear(readData, writeData) :
            //  interpolation === 'cubic' ? copyPixelBicubic(readData, writeData) :
            //  interpolation === 'lanczos' ? copyPixelLanczos(readData, writeData) :
            //  copyPixelNearest(readData, writeData);

            switch (interpolation)
            {
                case "linear": return CopyPixelBilinear;
                //case "cubic": return CopyPixelBilinear;
                //case "lanczos": return CopyPixelBilinear;
                default: return CopyPixelNearest;
            }
        }


        private void CopyPixelNearest(ImageData read, ImageData write, double xFrom, double yFrom, int to)
        {
            var nearest = GetReadIndex(
                  Clamp((int)Math.Round(xFrom), 0, read.Width - 1),
                  Clamp((int)Math.Round(yFrom), 0, read.Height - 1),
                  read.Width);

            for (var channel = 0; channel < 3; channel++)
            {
                write.Data[to + channel] = read.Data[nearest + channel];
            }
        }


        private void CopyPixelBilinear(ImageData read, ImageData write, double xFrom, double yFrom, int to)
        {
            var xl = Clamp((int)Math.Floor(xFrom), 0, read.Width - 1);
            var xr = Clamp((int)Math.Ceiling(xFrom), 0, read.Width - 1);
            var xf = xFrom - xl;

            var yl = Clamp((int)Math.Floor(yFrom), 0, read.Height - 1);
            var yr = Clamp((int)Math.Ceiling(yFrom), 0, read.Height - 1);
            var yf = yFrom - yl;

            var p00 = GetReadIndex(xl, yl, read.Width);
            var p10 = GetReadIndex(xr, yl, read.Width);
            var p01 = GetReadIndex(xl, yr, read.Width);
            var p11 = GetReadIndex(xr, yr, read.Width);

            for (var channel = 0; channel < 3; channel++)
            {
                var p0 = read.Data[p00 + channel] * (1 - xf) + read.Data[p10 + channel] * xf;
                var p1 = read.Data[p01 + channel] * (1 - xf) + read.Data[p11 + channel] * xf;
                write.Data[to + channel] = (byte)Math.Ceiling(p0 * (1 - yf) + p1 * yf);
            }
        }


        private delegate Vector3 CubeOrientationDelegate(double x, double y);


        private Vector3 GetPositiveZOrientation(double x, double y)
        {
            return new Vector3(-1, -x, -y);
        }


        private Vector3 GetNegativeZOrientation(double x, double y)
        {
            return new Vector3(1, x, -y);
        }


        private Vector3 GetPositiveXOrientation(double x, double y)
        {
            return new Vector3(x, -1, -y);
        }


        private Vector3 GetNegativeXOrientation(double x, double y)
        {
            return new Vector3(-x, 1, -y);
        }


        private Vector3 GetPositiveYOrientation(double x, double y)
        {
            return new Vector3(-y, -x, 1);
        }


        private Vector3 GetNegativeYOrientation(double x, double y)
        {
            return new Vector3(y, -x, -1);
        }


        private Vector3 GetUnknownOrientation(double x, double y)
        {
            return new Vector3(1, 1, 1);
        }


        private CubeOrientationDelegate GetOrientation(string faceName)
        {
            switch (faceName)
            {
                case PositiveZOrientationFaceName: return GetPositiveZOrientation;
                case NegativeZOrientationFaceName: return GetNegativeZOrientation;

                case PositiveXOrientationFaceName: return GetPositiveXOrientation;
                case NegativeXOrientationFaceName: return GetNegativeXOrientation;

                case PositiveYOrientationFaceName: return GetPositiveYOrientation;
                case NegativeYOrientationFaceName: return GetNegativeYOrientation;
            }

            return GetUnknownOrientation;
        }
    }
}