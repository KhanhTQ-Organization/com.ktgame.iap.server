using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using Prefs = UnityEngine.PlayerPrefs;

namespace com.unimob.iap.server
{
    public static class PlayerPrefs
    {
        private const int Iterations = 555;
        private const string StrPassword = "";
        private const string StrSalt = "";

        public static void DeleteAll()
        {
            Prefs.DeleteAll();
        }

        public static void DeleteKey(string key)
        {
            Prefs.DeleteKey(Encrypt(key, StrPassword));
        }

        public static float GetFloat(string key)
        {
            return GetFloat(key, 0.0f);
        }

        public static float GetFloat(string key, float defaultValue, bool isDecrypt = true)
        {
            float retValue = defaultValue;

            string strValue = GetString(key);

            if (float.TryParse(strValue, out retValue))
            {
                return retValue;
            }
            else
            {
                return defaultValue;
            }
        }

        public static int GetInt(string key)
        {
            return GetInt(key, 0);
        }

        public static int GetInt(string key, int defaultValue, bool isDecrypt = true)
        {
            int retValue = defaultValue;

            string strValue = GetString(key);

            if (int.TryParse(strValue, out retValue))
            {
                return retValue;
            }
            else
            {
                return defaultValue;
            }
        }


        public static string GetString(string key)
        {
            string strEncryptValue = GetRowString(key);
            return Decrypt(strEncryptValue, StrPassword);
        }

        public static string GetRowString(string key)
        {
            string strEncryptKey = Encrypt(key, StrPassword);
            string strEncryptValue = Prefs.GetString(strEncryptKey);
            return strEncryptValue;
        }

        public static string GetString(string key, string defaultValue)
        {
            string strEncryptValue = GetRowString(key, defaultValue);
            return Decrypt(strEncryptValue, StrPassword);
        }

        public static string GetRowString(string key, string defaultValue)
        {
            string strEncryptKey = Encrypt(key, StrPassword);
            string strEncryptDefaultValue = Encrypt(defaultValue, StrPassword);
            string strEncryptValue = Prefs.GetString(strEncryptKey, strEncryptDefaultValue);
            return strEncryptValue;
        }

        public static bool HasKey(string key)
        {
            return Prefs.HasKey(Encrypt(key, StrPassword));
        }

        public static void Save()
        {
            Prefs.Save();
        }

        public static void SetFloat(string key, float value)
        {
            string strValue = System.Convert.ToString(value);
            SetString(key, strValue);
        }

        public static void SetInt(string key, int value)
        {
            string strValue = System.Convert.ToString(value);
            SetString(key, strValue);
        }

        public static void SetString(string key, string value)
        {
            Prefs.SetString(Encrypt(key, StrPassword), Encrypt(value, StrPassword));
        }

        static byte[] GetIV()
        {
            byte[] IV = Encoding.UTF8.GetBytes(StrSalt);
            return IV;
        }

        static string Encrypt(string strPlain, string password)
        {
            try
            {
                DESCryptoServiceProvider des = new DESCryptoServiceProvider();

                Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, GetIV(), Iterations);

                byte[] key = rfc2898DeriveBytes.GetBytes(8);

                using (var memoryStream = new MemoryStream())
                using (var cryptoStream = new CryptoStream(memoryStream, des.CreateEncryptor(key, GetIV()), CryptoStreamMode.Write))
                {
                    memoryStream.Write(GetIV(), 0, GetIV().Length);

                    byte[] plainTextBytes = Encoding.UTF8.GetBytes(strPlain);

                    cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                    cryptoStream.FlushFinalBlock();

                    return Convert.ToBase64String(memoryStream.ToArray());
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Encrypt Exception: " + e);
                return strPlain;
            }
        }

        static string Decrypt(string strEncript, string password)
        {
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(strEncript);

                using (var memoryStream = new MemoryStream(cipherBytes))
                {
                    DESCryptoServiceProvider des = new DESCryptoServiceProvider();

                    byte[] iv = GetIV();
                    memoryStream.Read(iv, 0, iv.Length);

                    // use derive bytes to generate key from password and IV
                    var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, iv, Iterations);

                    byte[] key = rfc2898DeriveBytes.GetBytes(8);

                    using (var cryptoStream = new CryptoStream(memoryStream, des.CreateDecryptor(key, iv), CryptoStreamMode.Read))
                    using (var streamReader = new StreamReader(cryptoStream))
                    {
                        string strPlain = streamReader.ReadToEnd();
                        return strPlain;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Decrypt Exception: " + e);
                return strEncript;
            }
        }
    }
}