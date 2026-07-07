using System.Collections.Generic;
using System.Text;

namespace PFound.LocalizationService
{
    /// <summary>
    /// RFC-4180 CSV reader. Fields are comma-separated; a field wrapped in double quotes may contain
    /// commas, CR/LF line breaks, and escaped quotes (<c>""</c> → a single <c>"</c>). Records end at an
    /// unquoted CRLF or LF; a trailing newline does not create a spurious empty record. Returns a list
    /// of records, each a list of field strings.
    /// </summary>
    public static class CsvReader
    {
        public static List<List<string>> Parse(string text)
        {
            var records = new List<List<string>>();
            if (string.IsNullOrEmpty(text)) return records;

            var record = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            bool fieldStarted = false; // distinguishes a real empty trailing record from EOF

            int i = 0;
            int n = text.Length;
            while (i < n)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < n && text[i + 1] == '"') { field.Append('"'); i += 2; continue; }
                        inQuotes = false; i++; continue;
                    }
                    field.Append(c); i++; continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true; fieldStarted = true; i++; break;
                    case ',':
                        record.Add(field.ToString()); field.Length = 0; fieldStarted = true; i++; break;
                    case '\r':
                        // Treat CRLF and lone CR as one record terminator.
                        EndRecord(records, record, field, fieldStarted);
                        record = new List<string>(); field.Length = 0; fieldStarted = false;
                        if (i + 1 < n && text[i + 1] == '\n') i += 2; else i++;
                        break;
                    case '\n':
                        EndRecord(records, record, field, fieldStarted);
                        record = new List<string>(); field.Length = 0; fieldStarted = false;
                        i++;
                        break;
                    default:
                        field.Append(c); fieldStarted = true; i++; break;
                }
            }

            // Flush the last record if the input did not end with a newline.
            if (fieldStarted || field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString());
                records.Add(record);
            }

            return records;
        }

        private static void EndRecord(List<List<string>> records, List<string> record, StringBuilder field, bool fieldStarted)
        {
            record.Add(field.ToString());
            records.Add(record);
        }
    }
}
