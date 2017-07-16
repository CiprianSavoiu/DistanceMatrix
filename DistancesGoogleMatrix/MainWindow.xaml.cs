using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Device.Location;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LumenWorks.Framework.IO.Csv;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DistancesGoogleMatrix
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }


        private void RocketProcessing()
        {
          
            var methodTypes = new List<string> {"driving", "transit", "walking", "bicycling" };
            var exportTable = new DataTable();
            SetColumns(exportTable);

            var baseTable = ReadCsv(TxtBase.Text, ',', true);
            var targetTable = ReadCsv(TxtTarget.Text, ',', true);

            foreach (DataRow baseRow in baseTable.Rows)
            {
                var baseCoordinates = $"{baseRow["Latitude"]}, {baseRow["Longitude"]}";
                var baseId = baseRow["ID"].ToString();

                foreach (DataRow targetRow in targetTable.Rows)
                {
                    var targetCoordinates = $"{targetRow["Latitude"]}, {targetRow["Longitude"]}";
                    var targetId = targetRow["ID"].ToString();

                    foreach (var mType in methodTypes)
                    {
                        var sLat = double.Parse(baseRow["Latitude"].ToString());
                        var sLon = double.Parse(baseRow["Longitude"].ToString());
                        var eLat = double.Parse(targetRow["Latitude"].ToString());
                        var eLon = double.Parse(targetRow["Longitude"].ToString());
                        var gkey = ConfigurationManager.AppSettings["Server"];

                        var url = string.Format($"https://maps.googleapis.com/maps/api/distancematrix/json?origins={baseCoordinates}&destinations={targetCoordinates}&mode={mType}&sensor=false&key={gkey}");
                        var result = GetUrlContents(url);
                        var responseJson = JObject.Parse(result.ToString());
                        var firstAjToken = responseJson["rows"][0];
                        var dist = double.Parse(firstAjToken["elements"][0]["distance"]["value"].ToString());
                        var time = double.Parse(firstAjToken["elements"][0]["duration"]["value"].ToString()) / 60;
                        if (mType == "driving")
                        {
                            exportTable.Rows.Add();
                            exportTable.Rows[exportTable.Rows.Count - 1]["ID_base"] = baseId;
                            exportTable.Rows[exportTable.Rows.Count - 1]["ID_target"] = targetId;
                            exportTable.Rows[exportTable.Rows.Count - 1][2] = Math.Round(GetStraightDistance(sLat, sLon, eLat, eLon), 2);
                            exportTable.Rows[exportTable.Rows.Count - 1][3] = dist;
                            exportTable.Rows[exportTable.Rows.Count - 1][4] = Math.Round(time, 0);
                        }
                        if (mType == "transit")
                        {
                            exportTable.Rows[exportTable.Rows.Count - 1][5] = dist;
                            exportTable.Rows[exportTable.Rows.Count - 1][6] = Math.Round(time, 0);
                        }
                        if (mType == "walking")
                        {
                            exportTable.Rows[exportTable.Rows.Count - 1][7] = dist;
                            exportTable.Rows[exportTable.Rows.Count - 1][8] = Math.Round(time, 0);
                        }
                        else
                        {
                            exportTable.Rows[exportTable.Rows.Count - 1][9] = dist;
                            exportTable.Rows[exportTable.Rows.Count - 1][10] = Math.Round(time, 0);
                        }
                    }

                }


            }
            ExportCsv(exportTable);
            MessageBox.Show("Here u are!!!!!! Now u can travel");
        }

        private void SetColumns(DataTable exportTable)
        {
            exportTable.Columns.Add("ID_base");
            exportTable.Columns.Add("ID_target");
            exportTable.Columns.Add("straight line distance (m)");
            exportTable.Columns.Add("driving distance (m)");
            exportTable.Columns.Add("driving time (minutes)");
            exportTable.Columns.Add("public transport distance (m)");
            exportTable.Columns.Add("public transport time (minutes)");
            exportTable.Columns.Add("walking distance (m)");
            exportTable.Columns.Add("walking time (minute)");
            exportTable.Columns.Add("cycling distance (m)");
            exportTable.Columns.Add("cycling time (minutes)");
        }

        private double GetStraightDistance(double sLatitude, double sLongitude, double eLatitude, double eLongitude)
        {
            var sCoord = new GeoCoordinate(sLatitude, sLongitude);
            var eCoord = new GeoCoordinate(eLatitude, eLongitude);

            return sCoord.GetDistanceTo(eCoord);
        }


        private object GetUrlContents(string url)
        {
            //the downloaded content end up in the variable called content
            //var content = new MemoryStream();
            
            //initialize an HttpWebRequest for the current URL
            var webReq = (HttpWebRequest) WebRequest.Create(url);

            using (WebResponse response = webReq.GetResponse())
            {
                using (Stream responseStream = response.GetResponseStream())
                {
                    //responseStream.CopyTo(content);
                    return DeserializeFromStream(responseStream);
                }
            }
            //return content.ToArray();
        }
        private object DeserializeFromStream(Stream stream)
        {
            using (StreamReader sr = new StreamReader(stream))
            using(JsonReader reader = new JsonTextReader(sr))
            {
               JsonSerializer serializer = new JsonSerializer();
               // read the json from a stream
               // json size doesn't matter because only a small piece is read at a time from the HTTP request
               return serializer.Deserialize(reader);
            }
        }
        private void ExportCsv(DataTable dt)
        {
            var sb = new StringBuilder();

            var columnNames = dt.Columns.Cast<DataColumn>().
                Select(column => column.ColumnName).
                ToArray();
            sb.AppendLine(string.Join(",", columnNames));

            foreach (DataRow row in dt.Rows)
            {
                var fields = row.ItemArray.Select(field => field.ToString()).
                    ToArray();
                sb.AppendLine(string.Join(",", fields));
            }

            File.WriteAllText($"{System.IO.Path.GetDirectoryName(TxtBase.Text)}\\sample_data.csv", sb.ToString());

        }
        private static DataTable ReadCsv(string csvPath, char delimiter, bool hasHeader)
        {
            var csvTable = new DataTable();
            var id = 0;
            //open the file "data.csv" which is a CSV file with headers
            try
            {
                using (var csv = new CsvReader(new StreamReader(csvPath), hasHeader, delimiter))
                {
                    var fieldCount = csv.FieldCount;
                    var headers = csv.GetFieldHeaders();
                    if (headers.Length == 1)
                    {
                        csvTable.Columns.Add("wireid", typeof(int));
                        csvTable.Columns.Add("id", typeof(int));
                    }
                    else
                    {
                        //this bit could be modified to fine-tune the columns
                        foreach (var headerLabel in headers)
                            csvTable.Columns.Add(headerLabel, typeof(string));
                        //csvTable.Columns.Add("X_veg", typeof(double));
                        //csvTable.Columns.Add("Y_veg", typeof(double));
                        //csvTable.Columns.Add("Z_veg", typeof(double));
                        //csvTable.Columns.Add("Dist", typeof(double));
                        //csvTable.Columns.Add("X_wire", typeof(double));
                        //csvTable.Columns.Add("Y_wire", typeof(double));
                        //csvTable.Columns.Add("Z_wire", typeof(double));
                        //csvTable.Columns.Add("seid", typeof(int));
                        //csvTable.Columns.Add("ID", typeof(int));


                    }
                    while (csv.ReadNextRecord())
                    {
                        var newRow = csvTable.NewRow();
                        for (var i = 0; i < fieldCount; i++)
                        {
                            newRow[i] = csv[i];
                        }
                        csvTable.Rows.Add(newRow);
                    }

                    csvTable.Load(csv);
                }
            }
            catch (Exception e)
            {
                //throw new InvalidOperationException($"The program cannot open CSV file.\r\nThe following exception occured: {e.Message}");
                Debug.WriteLine("errrrrrrrrror");
            }
            //Console.WriteLine(veg_points);
            //veg_Table = csvTable;
            return csvTable;
        }


        private void BtnSelectBase_Click(object sender, RoutedEventArgs e)
        {
            //create a new file dialog which allow XML file only and set the window title
            var dialogShpSel = new System.Windows.Forms.OpenFileDialog
            {
                Title = @"Select Base file CSV",
                Filter = @"CSV|*.csv"
            };
            //write the file path to the textbox
            if (dialogShpSel.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var strfilename = dialogShpSel.FileName;
            TxtBase.Text = strfilename;
        }

        private void BtnSelectTarget_Click(object sender, RoutedEventArgs e)
        {
            //create a new file dialog which allow XML file only and set the window title
            var dialogShpSel = new System.Windows.Forms.OpenFileDialog
            {
                Title = @"Select Target file CSV",
                Filter = @"CSV|*.csv"
            };
            //write the file path to the textbox
            if (dialogShpSel.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var strfilename = dialogShpSel.FileName;
            TxtTarget.Text = strfilename;
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            RocketProcessing();
        }
    }
}
