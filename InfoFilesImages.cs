using System;
using System.Collections.Generic;
using System.Text;

namespace AppSANA
{
    public static class InfoFilesImages
    {

        /// <summary>
        /// Static value protected by access routine.
        /// </summary>
        static List<string[]> _globalListInfo;

        static List<List<string>> _blobGeneralListPrint;

        /// <summary>
        /// Access routine for global variable.
        /// </summary>
        public static List<string[]> GlobalListInfo
        {
            get
            {
                return _globalListInfo;
            }
            set
            {
                _globalListInfo = value;
            }
        }

        public static List<List<string>> GeneralListPrint
        {
            get
            {
                return _blobGeneralListPrint;
            }
            set
            {
                _blobGeneralListPrint = value;
            }
        }

        public static string getInfoOfImage(string id)
        {
            string result = "";
            foreach (string[] item in _globalListInfo)
            {
                if (item[9] == id)
                {
                    result = item[11] + "," + item[8];
                }
            }

            return result;
        }
    }
}