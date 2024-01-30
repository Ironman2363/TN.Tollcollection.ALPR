using cm;
using gx;

namespace TN.Tollcollection.ALPR.Entity
{
    /// <summary>
    /// Class khai báo các thư viện của ARH để việc khai báo chỉ cần làm 1 lần
    /// Tránh việc khai báo nhiều lần dẫn tới việc bị block key
    /// </summary>
    class ANPREntity
    {
        public cmAnpr anpr { get; set; }
        public gxImage gxImage { get; set; }
        public string path { get; set; }

        public ANPREntity(cmAnpr anpr, gxImage gxImage)
        {
            this.anpr = anpr;
            this.gxImage = gxImage;
        }
    }
}
