using System;
using System.Collections.Generic;

namespace Recipe_book.Helpers;

/// <summary>
/// Provides utility methods for text analysis and manipulation.
/// </summary>
public static class TextHelpers
{
    /// <summary>
    /// Computes the Levenshtein distance between two strings to identify typos or variations.
    /// </summary>
    public static int ComputeLevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
        if (string.IsNullOrEmpty(t)) return s.Length;

        int[] v0 = new int[t.Length + 1];
        int[] v1 = new int[t.Length + 1];

        for (int i = 0; i < v0.Length; i++) v0[i] = i;

        for (int i = 0; i < s.Length; i++)
        {
            v1[0] = i + 1;
            for (int j = 0; j < t.Length; j++)
            {
                int cost = (s[i] == t[j]) ? 0 : 1;
                v1[j + 1] = Math.Min(Math.Min(v1[j] + 1, v0[j + 1] + 1), v0[j] + cost);
            }
            for (int j = 0; j < v0.Length; j++) v0[j] = v1[j];
        }
        return v1[t.Length];
    }

    /// <summary>
    /// Analyzes a Hebrew word in plural form and returns possible singular forms.
    /// </summary>
    public static List<string> GetPossibleSingulars(string name)
    {
        var list = new List<string> { name };

        if (name.Length > 3)
        {
            if (name.EndsWith("ים"))
            {
                string baseName = name.Substring(0, name.Length - 2);
                list.Add(baseName); // e.g., מלפפונים -> מלפפון
                list.Add(baseName + "ה"); // e.g., ביצים -> ביצה
            }
            else if (name.EndsWith("ות"))
            {
                string baseName = name.Substring(0, name.Length - 2);
                list.Add(baseName + "ה"); // e.g., פיתות -> פיתה
                list.Add(baseName + "יה"); // e.g., עגבניות -> עגבניה
                list.Add(baseName); // e.g., פטריות -> פטרי
            }
        }
        return list;
    }
}