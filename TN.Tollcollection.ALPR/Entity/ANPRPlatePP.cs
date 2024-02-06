using Newtonsoft.Json.Linq;
using RestSharp.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TN.Tollcollection.ALPR.Entity
{
    /// <summary>
    /// Class tổng hợp dữ liệu của ParkPow (PP) trả về khi phân tích ảnh bằng API của PP
    /// </summary>
    public class ANPRPlatePP
    {
        public string filename { get; set; }
        public string timestamp { get; set; }
        public object camera_id { get; set; }
        public Result[] results { get; set; }
        public Usage usage { get; set; }
        public float processing_time { get; set; }



        public ANPRPlatePP()
        {

        }
        public ANPRPlatePP(ANPRPlatePPData data)
        {
            filename = data.filename;
            camera_id = data.camera_id;
            timestamp = data.timestamp_local;
            usage = new Usage();
            processing_time = DateTime.ParseExact(data.results.position_sec, "h:mm:ss.ffffff", CultureInfo.InvariantCulture).Millisecond;
            List<ResultData> resultDataList = new List<ResultData>();
            resultDataList.Add(data.results);
            List<Result> resultList1 = new List<Result>();
            foreach (ResultData resultData in resultDataList)
            {
                var result3 = new Result(resultData);
                resultList1.Add(result3);
            }
            results = resultList1.ToArray();
        }
    }


    public class Usage
    {
        public int calls { get; set; }
        public int max_calls { get; set; }
    }

    public class Result
    {
        public Box box { get; set; }
        public string plate { get; set; }
        public Region region { get; set; }
        public Vehicle vehicle { get; set; }
        public float score { get; set; }
        public Candidate[] candidates { get; set; }
        public float dscore { get; set; }
        public Model_Make[] model_make { get; set; }
        public Color[] color { get; set; }


        public Result()
        {

        }

        public Result(ResultData data)
        {
            box = new Box(data.plate.box);
            plate = data.plate.value;
            region = new Region(data.plate.props.regions);
            vehicle = new Vehicle(data.vehicle);
            score = data.plate.score;
            List<Candidate> candidateList = new List<Candidate>();
            foreach (PlateData plate in data.plate.props.plate)
            {
                var candidates1 = new Candidate(plate);
                candidateList.Add(candidates1);

            }
            candidates = candidateList.ToArray();
        }
    }

    public class Box
    {
        public int xmin { get; set; }
        public int ymin { get; set; }
        public int xmax { get; set; }
        public int ymax { get; set; }

        public Box()
        {

        }

        public Box(BoxData data)
        {
            xmin = data.xmin;
            ymin = data.ymin;
            xmax = data.xmax;
            ymax = data.ymax;
        }
    }

    public class Region
    {


        public string code { get; set; }
        public float score { get; set; }

        public Region()
        {

        }

        public Region(RegionsData data)
        {
            if (data.score != null)
            {
                score = (float)data.score;
            }
            else
            {
                score = 0;
            }

            if (data.value != null)
            {
                code = (string)data.value;
            }
            else
            {
                code = "";
            }

        }
    }

    public class Vehicle
    {
        public float score { get; set; }
        public string type { get; set; }
        public Box box { get; set; }

        public Vehicle()
        {

        }

        public Vehicle(VehicleData data)
        {
            if (data != null)
            {
                if (data.score != null)
                {
                    score = (float)data.score;
                }
                else
                {
                    score = 0;
                }

                if (data.type != null)
                {
                    type = data.type;
                }
                {
                    type = "";
                }

                if (data.box != null)
                {
                    box = new Box(data.box);
                }
                else
                {
                    box = new Box();
                }

            }
            else
            {
                score = 0;
                type = "";
                box = new Box();
            }

        }
    }

    public class Candidate
    {
        public float score { get; set; }
        public string plate { get; set; }

        public Candidate()
        {

        }

        public Candidate(PlateData data)
        {
            score = data.score;
            plate = data.value;
        }
    }

    public class Model_Make
    {
        public string make { get; set; }
        public string model { get; set; }
        public float score { get; set; }
    }

    public class Color
    {
        public string color { get; set; }
        public float score { get; set; }
    }

}
