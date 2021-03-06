//-----------------------------------------------------------------------
// <copyright file="ExcelClass.cs">
//     Copyright (c) 2016, 2020 Kent P. McKinney
//     Released under the terms of the MIT License
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using OfficeOpenXml;
using System.Text;
using ExcelDataReader;

namespace VehicleInformationLookupTool
{
    public class ExcelClass : IExcelClass, IDisposable
    {

        private static readonly List<string> VinIndicators = new List<string> { "vin", "vehicleidentificationnumber" };

        private IExcelDataReader _excelDataReader;

        private FileStream _fileStream;

        private DataSet _data;


        public void Dispose()
        {
            _excelDataReader?.Dispose();
            _excelDataReader = null;

            _fileStream?.Dispose();
            _fileStream = null;
            
            _data?.Dispose();
            _data = null;
        }


        public void CloseFile()
        {
            _excelDataReader?.Close();
            _fileStream?.Close();
        }


        public void ClearData() =>
            _data?.Clear();


        public List<string> GetColumnNames(string sheetName)
        {
            sheetName.ThrowIfNullOrEmpty();

            return (from DataTable table in _data.Tables where table.TableName == sheetName from DataColumn column in table.Columns select column.ColumnName).ToList();
        }
        

        public List<string> GetSheetNames() => 
            (from DataTable table in _data.Tables select table.TableName).ToList();


        public void OpenFile(string fileName)
        {
            /* Requirement: files with extensions .xls, .xlsx, and .csv must have matching content */

            /* Alert the user and return if the fileName is not valid */
            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
            {
                MessageBox.Show(fileName, "File Not Found Or Not Accessible");
                return;
            }

            try
            {
                _fileStream = File.Open(fileName, FileMode.Open, FileAccess.Read);

                /* Open the file with the appropriate method based on the file's extension */
                _excelDataReader = default;
                switch (Path.GetExtension(fileName).ToLower())
                {
                    case (".xls"):
                        _excelDataReader = ExcelReaderFactory.CreateBinaryReader(_fileStream);
                        break;
                    case (".xlsx"):
                        _excelDataReader = ExcelReaderFactory.CreateOpenXmlReader(_fileStream);
                        break;
                    case (".csv"):
                        _excelDataReader = ExcelReaderFactory.CreateCsvReader(_fileStream);
                        break;
                    default:
                        _excelDataReader = ExcelReaderFactory.CreateReader(_fileStream);
                        break;
                }

                /* Read data from the entire spreadsheet */
                _data = _excelDataReader?.AsDataSet(new ExcelDataSetConfiguration()
                {
                    ConfigureDataTable = reader => new ExcelDataTableConfiguration()
                    {
                        UseHeaderRow = true
                    }
                });
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show($"The file could not be accessed, possibly because permissions on the file do not allow it:\n\n{fileName}", "Unable to Open File");
            }
            catch (DirectoryNotFoundException)
            {
                MessageBox.Show($"The folder the file is located in cannot be found, possibly because the location is no longer accessible:\n\n{fileName}", "Unable to Open File");
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show($"The file could not be found:\n\n{fileName}", "Unable to Open File");
            }
            catch (IOException)
            {
                MessageBox.Show($"The file could not be opened, possibly because it is open in another program:\n\n{fileName}", "Unable to Open File");
            }
        }


        public DataTable GetDataTable(int worksheetIndex)
        {
            worksheetIndex.ThrowIfNullOrEmpty();

            return _data.Tables[worksheetIndex];
        }
        

        public int SheetLikelyToContainVins()
        {
            var sheets = GetSheetNames();
            if (sheets is null || sheets.Count <= 0)
            {
                return 0;
            }

            for (var i = 0; i < sheets.Count; i++)
            {
                var columnNames = GetColumnNames(sheets[i]);
                if (columnNames is null || columnNames.Count <= 0)
                {
                    continue;
                }

                /* Check if any columns in this sheet match any items in the list of vin indicators */
                if (columnNames.Any(name => VinIndicators.Contains(name, StringComparer.OrdinalIgnoreCase)))
                {
                    return i;
                }
            }

            return 0;
        }


        public int ColumnLikelyToContainVins(string sheetName)
        {
            sheetName.ThrowIfNullOrEmpty();

            /* Check if any columns in this sheet match any items in the list of vin indicators */
            var columnNames = this.GetColumnNames(sheetName);
            for (var i = 0; i < columnNames.Count; i++)
            {
                if (VinIndicators.Contains(columnNames[i], StringComparer.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return 0;
        }


        public bool SaveExcelFile(string saveFileName, in DataTable data)
        {
            saveFileName.ThrowIfNullOrEmpty();
            data.ThrowIfNullOrEmpty();

            try
            {
                using (var excel = new ExcelPackage())
                {
                    var worksheet = excel.Workbook.Worksheets.Add("Vehicle Information Lookup Tool");
                    worksheet.Cells["A1"].LoadFromDataTable(data, true);
                    worksheet.Cells.AutoFitColumns(0);
                    excel.SaveAs(new FileInfo(saveFileName));
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


        public bool SaveCsvFile(string saveFileName, in DataTable data)
        {
            saveFileName.ThrowIfNullOrEmpty();
            data.ThrowIfNullOrEmpty();

            try
            {
                using (var writer = new StreamWriter(saveFileName))
                {
                    var numColumns = data.Columns.Count;
                    var lastColumn = numColumns - 1;
                    const string delimiter = ",";

                    /* Add the header row */
                    var header = new StringBuilder();
                    for (var c = 0; c < data.Columns.Count; c++)
                    {
                        var columnName = data.Columns[c].ColumnName;

                        columnName = columnName.Replace(',', ' ');
                        header.Append(columnName);
                        if (c != lastColumn)
                        {
                            header.Append(delimiter);
                        }

                    }
                    writer.WriteLine(header);
                    writer.Flush();

                    /* Add data rows */
                    for (var r = 0; r < data.Rows.Count; r++)
                    {
                        var line = new StringBuilder();
                        var values = data.Rows[r].ItemArray;

                        for (var c = 0; c < data.Columns.Count; c++)
                        {
                            var value = values[c].ToString();
                            value = value.Replace(',',' ');
                            line.Append(value);
                            if (c != lastColumn)
                            {
                                line.Append(delimiter);
                            }
                        }

                        writer.WriteLine(line);
                        writer.Flush();
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}