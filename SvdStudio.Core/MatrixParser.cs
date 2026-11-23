using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
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

        // Remove comment lines before testing for MATLAB row separators.
        string trimmed = Regex.Replace(input.TrimStart('\uFEFF').ReplaceLineEndings("\n"), @"(?m)^[^\S\r\n]*(?:#|//)[^\r\n]*", "")
            .Trim(' ', '\r', '\n');
        if (trimmed.TrimStart().StartsWith('[')) trimmed = trimmed.TrimStart();

        // 1. JSON 2D array detection: [[ ... ]]
        if (trimmed.StartsWith('[') && trimmed[1..].TrimStart().StartsWith('['))
        {
            return ParseJson(trimmed);
        }

        // 2. MATLAB format detection: contains semicolons or enclosed in [...]
        if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
        {
            trimmed = trimmed[1..^1].Trim(' ', '\r', '\n');
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
        var rawLines = text.Split('\n');
        var rows = new List<double[]>();

        int expectedCols = -1;
        int lineNum = 0;

        foreach (var rawLine in rawLines)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(rawLine)) continue;
            double[] rowVals = ParseNumbers(rawLine, lineNum, allowLocalizedTabs: true);
            if (expectedCols == -1)
            {
                expectedCols = rowVals.Length;
            }
            else if (rowVals.Length != expectedCols)
            {
                throw new FormatException($"Row {rows.Count + 1} has {rowVals.Length} columns, expected {expectedCols} columns.");
            }

            rows.Add(rowVals);
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
            string rowStr = rowStrings[r];
            if (string.IsNullOrWhiteSpace(rowStr)) continue;
            double[] rowVals = ParseNumbers(rowStr, r + 1, allowLocalizedTabs: false);
            if (expectedCols == -1)
            {
                expectedCols = rowVals.Length;
            }
            else if (rowVals.Length != expectedCols)
            {
                throw new FormatException($"Row {r + 1} has {rowVals.Length} entries, expected {expectedCols}.");
            }

            rows.Add(rowVals);
        }

        if (rows.Count == 0) throw new FormatException("No valid matrix entries found.");
        return new Matrix(rows.ToArray());
    }

    private static double[] ParseNumbers(string input, int row, bool allowLocalizedTabs)
    {
        string line = input.Trim(' ', '\r', '\n');
        bool hasTabs = line.Contains('\t');
        bool localizedTabs = allowLocalizedTabs && hasTabs
            && CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == ",";
        string[] fields = hasTabs ? line.Split('\t') : line.Split(',');
        if (Array.Exists(fields, string.IsNullOrWhiteSpace)
            || (hasTabs && !localizedTabs && line.Contains(',') && Array.Exists(line.Split(','), string.IsNullOrWhiteSpace)))
            throw new FormatException($"Missing number in row {row}.");

        string pattern = localizedTabs ? @"\s+" : @"[,\s]+";
        var tokens = Regex.Split(line.Trim(), pattern);
        var values = new double[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && !(localizedTabs && double.TryParse(token, NumberStyles.Float, CultureInfo.CurrentCulture, out value)))
                throw new FormatException($"Invalid number '{token}' in row {row}.");
            if (!double.IsFinite(value))
                throw new FormatException($"Number '{token}' in row {row} is outside the finite double-precision range.");
            values[i] = value;
        }
        return values;
    }

    private static Matrix ParseJson(string json)
    {
        double[][]? rows;
        try
        {
            rows = JsonSerializer.Deserialize<double[][]>(json);
        }
        catch (JsonException ex)
        {
            throw new FormatException("Use a JSON array of numeric rows.", ex);
        }

        if (rows == null || rows.Length == 0 || rows[0] is not { Length: > 0 })
            throw new FormatException("JSON matrix must contain at least one nonempty row.");
        int columns = rows[0].Length;
        for (int row = 0; row < rows.Length; row++)
        {
            if (rows[row] == null || rows[row].Length != columns)
                throw new FormatException($"JSON row {row + 1} must contain exactly {columns} values.");
            if (Array.Exists(rows[row], n => !double.IsFinite(n)))
                throw new FormatException($"JSON row {row + 1} contains a nonfinite value.");
        }
        return new Matrix(rows);
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
