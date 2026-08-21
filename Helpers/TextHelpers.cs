using System;
using System.Collections.Generic;
using System.Linq;

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
                list.Add(baseName); // e.g., מלפפונים -> מלפפונ
                list.Add(baseName + "ה"); // e.g., ביצים -> ביצה

                // Fix Hebrew final letters (מנצפ"ך)
                if (baseName.Length > 0)
                {
                    char lastChar = baseName[baseName.Length - 1];
                    string baseWithoutLast = baseName.Substring(0, baseName.Length - 1);

                    if (lastChar == 'נ') list.Add(baseWithoutLast + "ן");
                    else if (lastChar == 'מ') list.Add(baseWithoutLast + "ם");
                    else if (lastChar == 'כ') list.Add(baseWithoutLast + "ך");
                    else if (lastChar == 'פ') list.Add(baseWithoutLast + "ף");
                    else if (lastChar == 'צ') list.Add(baseWithoutLast + "ץ");
                }
            }
            else if (name.EndsWith("ות"))
            {
                string baseName = name.Substring(0, name.Length - 2);
                list.Add(baseName + "ה"); // e.g., פיתות -> פיתה
                list.Add(baseName + "יה"); // e.g., עגבניות -> עגבניה
                list.Add(baseName); // e.g., פטריות -> פטרי
            }
        }

        return list.Distinct().ToList();
    }

    /// <summary>
    /// Parses free-text ingredients into Quantity, Unit, Name, and the RawText (without the quantity).
    /// </summary>
    public static (double Quantity, string Unit, string Name, string RawText) ParseIngredientText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (1, "יחידות", "", "");

        var words = input.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return (1, "יחידות", "", "");

        double quantity = 1;
        string unit = "יחידות";
        int currentIndex = 0;

        var textFractions = new Dictionary<string, double>
        {
            { "חצי", 0.5 }, { "רבע", 0.25 }, { "שליש", 0.33 },
            { "וחצי", 0.5 }, { "ורבע", 0.25 }, { "ושליש", 0.33 }
        };

        // Dictionary for Hebrew numbers written as text
        var hebrewNumbers = new Dictionary<string, double>
        {
            { "אחד", 1 }, { "אחת", 1 },
            { "שני", 2 }, { "שתי", 2 }, { "שניים", 2 }, { "שתיים", 2 }, { "שתים", 2 },
            { "שלוש", 3 }, { "שלושה", 3 }, { "שלושת", 3 },
            { "ארבע", 4 }, { "ארבעה", 4 }, { "ארבעת", 4 },
            { "חמש", 5 }, { "חמישה", 5 }, { "חמשת", 5 },
            { "שש", 6 }, { "שישה", 6 }, { "ששת", 6 },
            { "שבע", 7 }, { "שבעה", 7 }, { "שבעת", 7 },
            { "שמונה", 8 }, { "שמונת", 8 },
            { "תשע", 9 }, { "תשעה", 9 }, { "תשיעת", 9 },
            { "עשר", 10 }, { "עשרה", 10 }, { "עשרת", 10 }
        };

        var standardUnits = new Dictionary<string, string>
        {
            { "קילו", "ק״ג" }, { "קג", "ק״ג" }, { "ק\"ג", "ק״ג" }, { "ק'ג", "ק״ג" },
            { "גרם", "גרם" }, { "גר'", "גרם" },
            { "ליטר", "ליטר" }, { "ליטרים", "ליטר" },
            { "מל", "מ״ל" }, { "מ\"ל", "מ״ל" }, { "מ'ל", "מ״ל" }, { "מיליליטר", "מ״ל" },
            { "כוס", "כוס" }, { "כוסות", "כוס" },
            { "כף", "כף" }, { "כפות", "כף" },
            { "כפית", "כפית" }, { "כפיות", "כפית" },

            { "חבילה", "חבילה" }, { "חבילת", "חבילה" }, { "חבילות", "חבילה" },
            { "קופסה", "קופסה" }, { "קופסת", "קופסה" }, { "קופסאות", "קופסה" }, { "קופסא", "קופסה" }, { "קופסאת", "קופסה" },
            { "פחית", "פחית" }, { "פחיות", "פחית" },
            { "שקית", "שקית" }, { "שקיות", "שקית" },
            { "בקבוק", "בקבוק" }, { "בקבוקי", "בקבוק" }, { "בקבוקים", "בקבוק" },
            { "צנצנת", "צנצנת" }, { "צנצנות", "צנצנת" },
            { "מארז", "מארז" }, { "מארזי", "מארז" }, { "מארזים", "מארז" },
            { "קרטון", "קרטון" }, { "קרטוני", "קרטון" }, { "קרטונים", "קרטון" },
            { "ארגז", "ארגז" }, { "ארגזי", "ארגז" }, { "ארגזים", "ארגז" }
        };

        var stopWords = new HashSet<string> { "של" };

        // --- Step 1: Extract Quantity ---
        if (currentIndex < words.Length)
        {
            if (currentIndex + 1 < words.Length && words[currentIndex] == "שלושת" && words[currentIndex + 1] == "רבעי")
            {
                quantity = 0.75;
                currentIndex += 2;
            }
            else if (double.TryParse(words[currentIndex], out double parsedQty))
            {
                quantity = parsedQty;
                currentIndex++;

                if (currentIndex < words.Length && textFractions.ContainsKey(words[currentIndex]) && words[currentIndex].StartsWith("ו"))
                {
                    quantity += textFractions[words[currentIndex]];
                    currentIndex++;
                }
            }
            // Recognize numbers written as words (e.g. "שתי", "שלוש")
            else if (hebrewNumbers.ContainsKey(words[currentIndex]))
            {
                quantity = hebrewNumbers[words[currentIndex]];
                currentIndex++;

                if (currentIndex < words.Length && textFractions.ContainsKey(words[currentIndex]) && words[currentIndex].StartsWith("ו"))
                {
                    quantity += textFractions[words[currentIndex]];
                    currentIndex++;
                }
            }
            else if (textFractions.ContainsKey(words[currentIndex]) && !words[currentIndex].StartsWith("ו"))
            {
                quantity = textFractions[words[currentIndex]];
                currentIndex++;
            }
        }

        // --- Save index for raw text extraction ---
        int rawTextStartIndex = currentIndex;

        // --- Step 2: Identify Unit ---
        bool foundUnit = false;
        while (currentIndex < words.Length)
        {
            string possibleWord = words[currentIndex].Replace("\"", "").Replace("'", "");
            if (!foundUnit && standardUnits.ContainsKey(possibleWord))
            {
                unit = standardUnits[possibleWord];
                foundUnit = true;
                currentIndex++;
            }
            else if (!foundUnit && standardUnits.ContainsKey(words[currentIndex]))
            {
                unit = standardUnits[words[currentIndex]];
                foundUnit = true;
                currentIndex++;
            }
            else if (stopWords.Contains(possibleWord) || stopWords.Contains(words[currentIndex]))
            {
                currentIndex++;
            }
            else
            {
                break;
            }
        }

        // --- Step 3: Extract Clean Name and Raw Text ---
        string rawText = string.Join(" ", words.Skip(rawTextStartIndex)).Trim();
        string name = string.Join(" ", words.Skip(currentIndex).Where(w => w != "של")).Trim();

        return (quantity, unit, name, rawText);
    }
}