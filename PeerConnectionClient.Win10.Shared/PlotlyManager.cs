using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;

namespace PeerConnectionClient.Win10.Shared
{
    class PlotlyManager
    {
        private static volatile PlotlyManager _instance;
        private static readonly object SyncRoot = new object();

        private readonly string username = "hookflash";
        private readonly string password = "QGC4s8XjbPmZoq";
        private readonly string key = "hei6r5e6rd";
        private readonly string email = "erik@hookflash.com";
        private readonly string restAPIUrl = "https://plot.ly/clientresp";
        public static PlotlyManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                lock (SyncRoot)
                {
                    if (_instance == null) _instance = new PlotlyManager();
                }

                return _instance;
            }
        }

        public async void SendData(List<object> dict)
        {
            using (var client = new HttpClient())
            {
                var values = new Dictionary<string, string>
                {
                   { "un", username },
                   { "key", key },
                   {"origin", "plot" },
                    {"platform","lisp"},
                    { "args","[[0, 1, 2], [3, 1, 6]]"},
                    {"kwargs","{\"filename\": \"plot from api\",\"fileopt\": \"overwrite\",\"style\": {\"type\": \"bar\"}, \"traces\": [0,3,5],\"layout\": {\"title\": \"experimental data\"},\"world_readable\": true}"},
                };
                /*var values = new Dictionary<string, string>
                {
                   { "un", username },
                   { "key", key },
                   {"origin", "plot" },
                    {"platform","lisp"},
                    { "args","[[0, 1, 2], [3, 1, 6]],[{\"x\": [0, 1, 2], \"y\": [3, 1, 6]}],[{\"x\": [0, 1, 2], \"y\": [3, 1, 6], \"name\": \"Experimental\", \"marker\": {\"symbol\": \"square\", \"color\": \"purple\"}}, {\"x\": [1, 2, 3], \"y\": [3, 4, 5], \"name\": \"Control\"}]"},
                    {"kwargs","{\"filename\": \"plot from api\",\"fileopt\": \"overwrite\",\"style\": {\"type\": \"bar\"}, \"traces\": [0,3,5],\"layout\": {\"title\": \"experimental data\"},\"world_readable\": true}"},
                };*/

                var content = new FormUrlEncodedContent(values);

                var response = await client.PostAsync(restAPIUrl, content);

                var responseString = await response.Content.ReadAsStringAsync();
                if (responseString != null && responseString.Length > 0)
                {
                    Debug.Write(responseString);
                }
            }
        }
    }
}
