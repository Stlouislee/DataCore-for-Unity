using System.Collections.Generic;
using System.Text;

namespace AroAro.DataCore.Import
{
    /// <summary>
    /// RFC 4180 compliant CSV parser
    /// </summary>
    public static class CsvParser
    {
        /// <summary>
        /// Parse a single CSV line respecting quoted fields
        /// </summary>
        public static List<string> ParseLine(string line, char delimiter = ',')
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delimiter && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            result.Add(sb.ToString());
            return result;
        }

        /// <summary>
        /// Parse a single CSV line into a string array
        /// </summary>
        public static string[] ParseLineToArray(string line, char delimiter = ',')
        {
            return ParseLine(line, delimiter).ToArray();
        }

        /// <summary>
        /// Full CSV text parsing (supports newlines inside quoted fields)
        /// </summary>
        public static List<List<string>> ParseAll(string csvText, char delimiter = ',')
        {
            var rows = new List<List<string>>();
            var current = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < csvText.Length; i++)
            {
                char c = csvText[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csvText.Length && csvText[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == delimiter)
                    {
                        current.Add(field.ToString());
                        field.Clear();
                    }
                    else if (c == '\n' || c == '\r')
                    {
                        current.Add(field.ToString());
                        field.Clear();
                        if (current.Count > 0) rows.Add(current);
                        current = new List<string>();
                        if (c == '\r' && i + 1 < csvText.Length && csvText[i + 1] == '\n') i++;
                    }
                    else
                    {
                        field.Append(c);
                    }
                }
            }

            current.Add(field.ToString());
            if (current.Count > 0 && !(current.Count == 1 && string.IsNullOrEmpty(current[0])))
                rows.Add(current);

            return rows;
        }
    }
}
