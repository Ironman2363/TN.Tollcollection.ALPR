using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TN.Tollcollection.ALPR.Entity
{
    public class ANPRPlatePPData
    {
        public string camera_id { get; set; }
        public string filename { get; set; }
        public ResultData results { get; set; }
        public string timestamp_local { get; set; }


    }

    public class ResultData
    {
        public DirectionData direction { get; set; }
        public PlateData plate { get; set; }
        public string position_sec { get; set; }
        public string source_url { get; set; }
        public VehicleData vehicle { get; set; }
    }

    public class DirectionData
    {
        public object value { get; set; }
    }

    public class PlateData
    {
        public BoxData box { get; set; }
        public PropsData props { get; set; }
        public float score { get; set; }
        public string type { get; set; }
        public string value { get; set; }



    }

    public class BoxData
    {
        public int xmax { get; set; }
        public int xmin { get; set; }
        public int ymax { get; set; }
        public int ymin { get; set; }


    }

    public class PropsData
    {
        public PlateData[] plate { get; set; }
        public RegionsData regions { get; set; }


    }

    public class RegionsData
    {
        public object score { get; set; }
        public object value { get; set; }

    }

    public class VehicleData
    {
        public object score { get; set; }
        public string type { get; set; }
        public BoxData box { get; set; }

    }


}
