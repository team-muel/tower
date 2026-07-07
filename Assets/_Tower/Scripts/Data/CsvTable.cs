using System;
using System.Collections.Generic;

namespace Tower.Data
{
    // Minimal, quote/comma-safe CSV reader. Operates on in-memory text
    // (Unity TextAsset.text) — no file IO. RFC-4180-ish:
    //  - fields separated by ',',
    //  - a field may be wrapped in double quotes to contain ',' or newlines,
    //  - "" inside a quoted field is a literal quote.
    // First non-empty line is treated as the header row. Blank trailing lines
    // are ignored. Rows must have exactly the header's column count.
    public sealed class CsvTable
    {
        public string SheetName { get; }
        public IReadOnlyList<string> Header { get; }

        // One entry per data row. Each row maps columnName -> raw cell value.
        public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows { get; }

        private CsvTable(
            string sheetName,
            IReadOnlyList<string> header,
            IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
        {
            SheetName = sheetName;
            Header = header;
            Rows = rows;
        }

        public static CsvTable Parse(string sheetName, string text)
        {
            if (text == null)
            {
                throw new DataValidationException(
                    "Sheet '" + sheetName + "': CSV text is null.");
            }

            var records = SplitRecords(text);
            if (records.Count == 0)
            {
                throw new DataValidationException(
                    "Sheet '" + sheetName + "': CSV has no header row.");
            }

            var header = records[0];
            var rows = new List<IReadOnlyDictionary<string, string>>(records.Count - 1);
            for (int r = 1; r < records.Count; r++)
            {
                var fields = records[r];
                if (fields.Count != header.Count)
                {
                    throw new DataValidationException(
                        "Sheet '" + sheetName + "' row " + (r + 1) +
                        ": expected " + header.Count + " columns but found " + fields.Count + ".");
                }

                var map = new Dictionary<string, string>(header.Count, StringComparer.Ordinal);
                for (int c = 0; c < header.Count; c++)
                {
                    map[header[c]] = fields[c];
                }
                rows.Add(map);
            }

            return new CsvTable(sheetName, header, rows);
        }

        // Splits the whole document into records (rows), each a list of fields.
        // Handles quoted fields spanning commas / newlines. Skips fully blank rows.
        private static List<List<string>> SplitRecords(string text)
        {
            var records = new List<List<string>>();
            var current = new List<string>();
            var field = new System.Text.StringBuilder();
            bool inQuotes = false;
            bool sawAnyChar = false;

            int i = 0;
            int n = text.Length;
            while (i < n)
            {
                char ch = text[i];

                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < n && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i += 2;
                            continue;
                        }
                        inQuotes = false;
                        i++;
                        continue;
                    }
                    field.Append(ch);
                    i++;
                    continue;
                }

                if (ch == '"')
                {
                    inQuotes = true;
                    sawAnyChar = true;
                    i++;
                    continue;
                }

                if (ch == ',')
                {
                    current.Add(field.ToString());
                    field.Length = 0;
                    sawAnyChar = true;
                    i++;
                    continue;
                }

                if (ch == '\r' || ch == '\n')
                {
                    // Consume CRLF as a single line break.
                    if (ch == '\r' && i + 1 < n && text[i + 1] == '\n')
                    {
                        i += 2;
                    }
                    else
                    {
                        i++;
                    }

                    current.Add(field.ToString());
                    field.Length = 0;

                    // Skip fully-empty lines (single empty field, no content seen).
                    if (!(current.Count == 1 && current[0].Length == 0 && !sawAnyChar))
                    {
                        records.Add(current);
                    }
                    current = new List<string>();
                    sawAnyChar = false;
                    continue;
                }

                field.Append(ch);
                sawAnyChar = true;
                i++;
            }

            // Flush trailing field/record if the file did not end with a newline.
            if (sawAnyChar || field.Length > 0 || current.Count > 0)
            {
                current.Add(field.ToString());
                if (!(current.Count == 1 && current[0].Length == 0 && !sawAnyChar))
                {
                    records.Add(current);
                }
            }

            return records;
        }
    }
}
