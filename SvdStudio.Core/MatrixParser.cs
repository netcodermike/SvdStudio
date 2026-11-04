using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SvdStudio.Core;

/// <summary>
/// Flexible and resilient parser for matrices from text, CSV, TSV, MATLAB syntax, and JSON arrays.
/// </summary>
public static class MatrixParser
{
    /// <summary>
    /// Parses a matrix from text input.
    /// Handles:
    /// - Space/tab/comma delimited rows with newlines
    /// - MATLAB format: [1 2 3; 4 5 6] or [1, 2, 3; 4, 5, 6]
    /// - JSON format: [[1, 2], [3, 4]]
    /// - CSV / TSV with optional empty lines and comments (# or //)
    /// </summary>
    public static Matrix Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new FormatException("Input string is empty.");

        string trimmed = input.Trim();

        // 1. JSON 2D array detection: [[ ... ]]
        if (trimmed.StartsWith("[[") && trimmed.EndsWith("]]"))
        {
            return ParseJson(trimmed);
        }

        // 2. MATLAB format detection: contains semicolons or enclosed in [...]
        if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
        {
            trimmed = trimmed[1..^1].Trim();
        }

        // If semicolons separate rows (MATLAB style)
        if (trimmed.Contains(';'))
        {
            return ParseMatlabSemicolons(trimmed);
        }

        // 3. Line-by-line format (standard CSV, TSV, or whitespace-separated)
        return ParseLineByLine(trimmed);
    }

    private static Matrix ParseLineByLine(string text)
    {
        var rawLines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var rows = new List<double[]>();

        int expectedCols = -1;
        int lineNum = 0;

        foreach (var rawLine in rawLines)
        {
            lineNum++;
            var line = rawLine.Trim();

            // Ignore blank lines and comments
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                continue;

            // Remove trailing commas/semicolons
            line = line.TrimEnd(',', ';');

            // Split line by comma, tab, or whitespace
            var tokens = Regex.Split(line, @"[,\t\s]+");
            var rowVals = new List<double>();

            foreach (var tok in tokens)
            {
                if (string.IsNullOrWhiteSpace(tok)) continue;

                if (!double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                {
                    // Fallback to current culture in case user used comma decimal separator in non-comma delimiter
                    if (!double.TryParse(tok, NumberStyles.Float, CultureInfo.CurrentCulture, out val))
                    {
                        throw new FormatException($"Invalid number '{tok}' at line {lineNum}.");
                    }
                }
                rowVals.Add(val);
            }

            if (rowVals.Count == 0) continue;

            if (expectedCols == -1)
            {
                expectedCols = rowVals.Count;
            }
            else if (rowVals.Count != expectedCols)
            {
                throw new FormatException($"Row {rows.Count + 1} has {rowVals.Count} columns, expected {expectedCols} columns.");
            }

            rows.Add(rowVals.ToArray());
        }

        if (rows.Count == 0 || expectedCols <= 0)
            throw new FormatException("No valid numeric rows found in input.");

        return new Matrix(rows.ToArray());
    }

    private static Matrix ParseMatlabSemicolons(string text)
    {
        var rowStrings = text.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var rows = new List<double[]>();
        int expectedCols = -1;

        for (int r = 0; r < rowStrings.Length; r++)
        {
            var rowStr = rowStrings[r].Trim();
            if (string.IsNullOrWhiteSpace(rowStr)) continue;

            var tokens = Regex.Split(rowStr, @"[,\t\s]+");
            var rowVals = new List<double>();

            foreach (var tok in tokens)
            {
                if (string.IsNullOrWhiteSpace(tok)) continue;
                if (!double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                {
                    throw new FormatException($"Invalid number '{tok}' in row {r + 1}.");
                }
                rowVals.Add(val);
            }

            if (rowVals.Count == 0) continue;

            if (expectedCols == -1)
            {
                expectedCols = rowVals.Count;
            }
            else if (rowVals.Count != expectedCols)
            {
                throw new FormatException($"Row {r + 1} has {rowVals.Count} entries, expected {expectedCols}.");
            }

            rows.Add(rowVals.ToArray());
        }

        if (rows.Count == 0) throw new FormatException("No valid matrix entries found.");
        return new Matrix(rows.ToArray());
    }

    private static Matrix ParseJson(string json)
    {
        // Simple regex matching for 2D numeric arrays [[1, 2], [3, 4]]
        var rowMatches = Regex.Matches(json, @"\[\s*([^\[\]]+?)\s*\]");
        var rows = new List<double[]>();
        int expectedCols = -1;

        foreach (Match match in rowMatches)
        {
            var content = match.Groups[1].Value;
            var tokens = content.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var rowVals = new List<double>();

            foreach (var tok in tokens)
            {
                var trimmedTok = tok.Trim();
                if (double.TryParse(trimmedTok, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                {
                    rowVals.Add(val);
                }
                else
                {
                    throw new FormatException($"Invalid number in JSON array: '{trimmedTok}'.");
                }
            }

            if (rowVals.Count == 0) continue;

            if (expectedCols == -1)
            {
                expectedCols = rowVals.Count;
            }
            else if (rowVals.Count != expectedCols)
            {
                throw new FormatException($"JSON row {rows.Count + 1} length mismatch: got {rowVals.Count}, expected {expectedCols}.");
            }

            rows.Add(rowVals.ToArray());
        }

        if (rows.Count == 0)
            throw new FormatException("No valid rows could be extracted from JSON.");

        return new Matrix(rows.ToArray());
    }

    /// <summary>
    /// Attempts to parse the input, returning false if it fails.
    /// </summary>
    public static bool TryParse(string input, out Matrix? result, out string? errorMessage)
    {
        try
        {
            result = Parse(input);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            result = null;
            errorMessage = ex.Message;
            return false;
        }
    }
}
