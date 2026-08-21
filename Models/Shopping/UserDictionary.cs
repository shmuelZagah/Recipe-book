using SQLite;

namespace Recipe_book.Models.Shopping
{
    public class UserDictionary
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // מפתח החיפוש
        public string ItemBaseName { get; set; } // למשל: "ביצה" או "שוקולד"
        public string UnitBaseName { get; set; } // למשל: "קרטון" או "חבילה"

        // מה שהמערכת למדה
        public string SingularDisplay { get; set; } // למשל: "קרטון ביצים"
        public string PluralDisplay { get; set; }   // למשל: "קרטונים של ביצים"
    }
}