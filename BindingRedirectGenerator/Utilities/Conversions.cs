﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BindingRedirectGenerator.Utilities
{
    public static class Conversions
    {
        private static readonly char[] _enumSeparators = new char[] { ',', ';', '+', '|', ' ' };

        public static Type GetEnumeratedType(Type collectionType)
        {
            if (collectionType == null)
                throw new ArgumentNullException(nameof(collectionType));

            foreach (var type in collectionType.GetInterfaces())
            {
                if (!type.IsGenericType)
                    continue;

                if (type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return type.GetGenericArguments()[0];

                if (type.GetGenericTypeDefinition() == typeof(ICollection<>))
                    return type.GetGenericArguments()[0];

                if (type.GetGenericTypeDefinition() == typeof(IList<>))
                    return type.GetGenericArguments()[0];
            }
            return null;
        }

        public static long ToPositiveFileTime(DateTime dt)
        {
            var ft = ToFileTimeUtc(dt.ToUniversalTime());
            return ft < 0 ? 0 : ft;
        }

        public static long ToPositiveFileTimeUtc(DateTime dt)
        {
            var ft = ToFileTimeUtc(dt);
            return ft < 0 ? 0 : ft;
        }

        // can return negative numbers
        public static long ToFileTime(DateTime dt) => ToFileTimeUtc(dt.ToUniversalTime());
        public static long ToFileTimeUtc(DateTime dt)
        {
            const long ticksPerMillisecond = 10000;
            const long ticksPerSecond = ticksPerMillisecond * 1000;
            const long ticksPerMinute = ticksPerSecond * 60;
            const long ticksPerHour = ticksPerMinute * 60;
            const long ticksPerDay = ticksPerHour * 24;
            const int daysPerYear = 365;
            const int daysPer4Years = daysPerYear * 4 + 1;
            const int daysPer100Years = daysPer4Years * 25 - 1;
            const int daysPer400Years = daysPer100Years * 4 + 1;
            const int daysTo1601 = daysPer400Years * 4;
            const long fileTimeOffset = daysTo1601 * ticksPerDay;
            var ticks = dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime().Ticks : dt.Ticks;
            ticks -= fileTimeOffset;
            return ticks;
        }

        public static Guid ComputeGuidHash(string text)
        {
            if (text == null)
                return Guid.Empty;

            using var md5 = MD5.Create();
            return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        public static byte[] ToBytesFromHexa(string text)
        {
            if (text == null)
                return null;

            var list = new List<byte>();
            var lo = false;
            byte prev = 0;
            int offset;

            // handle 0x or 0X notation
            if (text.Length >= 2 && text[0] == '0' && (text[1] == 'x' || text[1] == 'X'))
            {
                offset = 2;
            }
            else
            {
                offset = 0;
            }

            for (var i = 0; i < text.Length - offset; i++)
            {
                var b = GetHexaByte(text[i + offset]);
                if (b == 0xFF)
                    continue;

                if (lo)
                {
                    list.Add((byte)(prev * 16 + b));
                }
                else
                {
                    prev = b;
                }
                lo = !lo;
            }
            return list.ToArray();
        }

        public static byte GetHexaByte(char c)
        {
            if (c >= '0' && c <= '9')
                return (byte)(c - '0');

            if (c >= 'A' && c <= 'F')
                return (byte)(c - 'A' + 10);

            if (c >= 'a' && c <= 'f')
                return (byte)(c - 'a' + 10);

            return 0xFF;
        }

        public static IList<T> SplitToList<T>(string text, params char[] separators) => SplitToList<T>(text, null, separators);
        public static IList<T> SplitToList<T>(string text, IFormatProvider provider, params char[] separators)
        {
            var al = new List<T>();
            if (text == null || separators == null || separators.Length == 0)
                return al;

            foreach (string s in text.Split(separators))
            {
                var value = s.Nullify();
                if (value == null)
                    continue;

                var item = ChangeType(value, default(T), provider);
                al.Add(item);
            }
            return al;
        }

        public static bool EqualsIgnoreCase(this string thisString, string text) => EqualsIgnoreCase(thisString, text, false);
        public static bool EqualsIgnoreCase(this string thisString, string text, bool trim)
        {
            if (trim)
            {
                thisString = thisString.Nullify();
                text = text.Nullify();
            }

            if (thisString == null)
                return text == null;

            if (text == null)
                return false;

            if (thisString.Length != text.Length)
                return false;

            return string.Compare(thisString, text, StringComparison.OrdinalIgnoreCase) == 0;
        }

        public static string Nullify(this string text)
        {
            if (text == null)
                return null;

            if (string.IsNullOrWhiteSpace(text))
                return null;

            var t = text.Trim();
            return t.Length == 0 ? null : t;
        }

        public static object ChangeType(object input, Type conversionType) => ChangeType(input, conversionType, null, null);
        public static object ChangeType(object input, Type conversionType, object defaultValue) => ChangeType(input, conversionType, defaultValue, null);
        public static object ChangeType(object input, Type conversionType, object defaultValue, IFormatProvider provider)
        {
            if (!TryChangeType(input, conversionType, provider, out var value))
                return defaultValue;

            return value;
        }

        public static T ChangeType<T>(object input) => ChangeType(input, default(T));
        public static T ChangeType<T>(object input, T defaultValue) => ChangeType(input, defaultValue, null);
        public static T ChangeType<T>(object input, T defaultValue, IFormatProvider provider)
        {
            if (!TryChangeType(input, provider, out T value))
                return defaultValue;

            return value;
        }

        public static bool TryChangeType<T>(object input, out T value) => TryChangeType(input, null, out value);
        public static bool TryChangeType<T>(object input, IFormatProvider provider, out T value)
        {
            if (!TryChangeType(input, typeof(T), provider, out var tvalue))
            {
                value = default;
                return false;
            }

            value = (T)tvalue;
            return true;
        }

        public static bool TryChangeType(object input, Type conversionType, out object value) => TryChangeType(input, conversionType, null, out value);
        public static bool TryChangeType(object input, Type conversionType, IFormatProvider provider, out object value)
        {
            if (conversionType == null)
                throw new ArgumentNullException(nameof(conversionType));

            if (conversionType == typeof(object))
            {
                value = input;
                return true;
            }

            value = conversionType.IsValueType ? Activator.CreateInstance(conversionType) : null;
            if (input == null)
                return !conversionType.IsValueType;

            var inputType = input.GetType();
            if (conversionType.IsAssignableFrom(inputType))
            {
                value = input;
                return true;
            }

            if (conversionType.IsEnum)
                return EnumTryParse(conversionType, input, out value);

            if (conversionType == typeof(Guid))
            {
                var svalue = string.Format(provider, "{0}", input).Nullify();
                if (svalue != null && Guid.TryParse(svalue, out var guid))
                {
                    value = guid;
                    return true;
                }
                return false;
            }

            if (conversionType == typeof(IntPtr))
            {
                if (IntPtr.Size == 8)
                {
                    if (TryChangeType(input, provider, out long l))
                    {
                        value = new IntPtr(l);
                        return true;
                    }
                }
                else if (TryChangeType(input, provider, out int i))
                {
                    value = new IntPtr(i);
                    return true;
                }
                return false;
            }

            if (conversionType == typeof(int))
            {
                if (inputType == typeof(uint))
                {
                    value = unchecked((int)(uint)input);
                    return true;
                }

                if (inputType == typeof(ulong))
                {
                    value = unchecked((int)(ulong)input);
                    return true;
                }

                if (inputType == typeof(ushort))
                {
                    value = unchecked((int)(ushort)input);
                    return true;
                }

                if (inputType == typeof(byte))
                {
                    value = unchecked((int)(byte)input);
                    return true;
                }
            }

            if (conversionType == typeof(long))
            {
                if (inputType == typeof(uint))
                {
                    value = unchecked((long)(uint)input);
                    return true;
                }

                if (inputType == typeof(ulong))
                {
                    value = unchecked((long)(ulong)input);
                    return true;
                }

                if (inputType == typeof(ushort))
                {
                    value = unchecked((long)(ushort)input);
                    return true;
                }

                if (inputType == typeof(byte))
                {
                    value = unchecked((long)(byte)input);
                    return true;
                }
            }

            if (conversionType == typeof(short))
            {
                if (inputType == typeof(uint))
                {
                    value = unchecked((short)(uint)input);
                    return true;
                }

                if (inputType == typeof(ulong))
                {
                    value = unchecked((short)(ulong)input);
                    return true;
                }

                if (inputType == typeof(ushort))
                {
                    value = unchecked((short)(ushort)input);
                    return true;
                }

                if (inputType == typeof(byte))
                {
                    value = unchecked((short)(byte)input);
                    return true;
                }
            }

            if (conversionType == typeof(sbyte))
            {
                if (inputType == typeof(uint))
                {
                    value = unchecked((sbyte)(uint)input);
                    return true;
                }

                if (inputType == typeof(ulong))
                {
                    value = unchecked((sbyte)(ulong)input);
                    return true;
                }

                if (inputType == typeof(ushort))
                {
                    value = unchecked((sbyte)(ushort)input);
                    return true;
                }

                if (inputType == typeof(byte))
                {
                    value = unchecked((sbyte)(byte)input);
                    return true;
                }
            }

            if (conversionType == typeof(uint))
            {
                if (inputType == typeof(int))
                {
                    value = unchecked((uint)(int)input);
                    return true;
                }

                if (inputType == typeof(long))
                {
                    value = unchecked((uint)(long)input);
                    return true;
                }

                if (inputType == typeof(short))
                {
                    value = unchecked((uint)(short)input);
                    return true;
                }

                if (inputType == typeof(sbyte))
                {
                    value = unchecked((uint)(sbyte)input);
                    return true;
                }
            }

            if (conversionType == typeof(ulong))
            {
                if (inputType == typeof(int))
                {
                    value = unchecked((ulong)(int)input);
                    return true;
                }

                if (inputType == typeof(long))
                {
                    value = unchecked((ulong)(long)input);
                    return true;
                }

                if (inputType == typeof(short))
                {
                    value = unchecked((ulong)(short)input);
                    return true;
                }

                if (inputType == typeof(sbyte))
                {
                    value = unchecked((ulong)(sbyte)input);
                    return true;
                }
            }

            if (conversionType == typeof(ushort))
            {
                if (inputType == typeof(int))
                {
                    value = unchecked((ushort)(int)input);
                    return true;
                }

                if (inputType == typeof(long))
                {
                    value = unchecked((ushort)(long)input);
                    return true;
                }

                if (inputType == typeof(short))
                {
                    value = unchecked((ushort)(short)input);
                    return true;
                }

                if (inputType == typeof(sbyte))
                {
                    value = unchecked((ushort)(sbyte)input);
                    return true;
                }
            }

            if (conversionType == typeof(byte))
            {
                if (inputType == typeof(int))
                {
                    value = unchecked((byte)(int)input);
                    return true;
                }

                if (inputType == typeof(long))
                {
                    value = unchecked((byte)(long)input);
                    return true;
                }

                if (inputType == typeof(short))
                {
                    value = unchecked((byte)(short)input);
                    return true;
                }

                if (inputType == typeof(sbyte))
                {
                    value = unchecked((byte)(sbyte)input);
                    return true;
                }
            }

            if (input is IConvertible convertible)
            {
                try
                {
                    value = convertible.ToType(conversionType, provider);
                    return true;
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch
                {
                    return false;
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }

            if (conversionType == typeof(string))
            {
                value = string.Format(provider, "{0}", input);
                return true;
            }

            return false;
        }

        public static ulong EnumToUInt64(string text, Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            return EnumToUInt64(ChangeType(text, enumType));
        }

        public static ulong EnumToUInt64(object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var typeCode = Convert.GetTypeCode(value);
            switch (typeCode)
            {
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                    return (ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture);

                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return Convert.ToUInt64(value, CultureInfo.InvariantCulture);

                case TypeCode.String:
                default:
                    return ChangeType<ulong>(value, 0, CultureInfo.InvariantCulture);
            }
        }

        private static bool StringToEnum(Type type, string[] names, Array values, string input, out object value)
        {
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i].EqualsIgnoreCase(input))
                {
                    value = values.GetValue(i);
                    return true;
                }
            }

            for (var i = 0; i < values.GetLength(0); i++)
            {
                var valuei = values.GetValue(i);
                if (input.Length > 0 && input[0] == '-')
                {
                    var ul = (long)EnumToUInt64(valuei);
                    if (ul.ToString(CultureInfo.CurrentCulture).EqualsIgnoreCase(input))
                    {
                        value = valuei;
                        return true;
                    }
                }
                else
                {
                    var ul = EnumToUInt64(valuei);
                    if (ul.ToString(CultureInfo.CurrentCulture).EqualsIgnoreCase(input))
                    {
                        value = valuei;
                        return true;
                    }
                }
            }

            if (char.IsDigit(input[0]) || input[0] == '-' || input[0] == '+')
            {
                var obj = EnumToObject(type, input);
                if (obj == null)
                {
                    value = Activator.CreateInstance(type);
                    return false;
                }
                value = obj;
                return true;
            }

            value = Activator.CreateInstance(type);
            return false;
        }

        public static object EnumToObject(Type enumType, object value)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            if (!enumType.IsEnum)
                throw new ArgumentException(null, nameof(enumType));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var underlyingType = Enum.GetUnderlyingType(enumType);
            if (underlyingType == typeof(long))
                return Enum.ToObject(enumType, ChangeType<long>(value));

            if (underlyingType == typeof(ulong))
                return Enum.ToObject(enumType, ChangeType<ulong>(value));

            if (underlyingType == typeof(int))
                return Enum.ToObject(enumType, ChangeType<int>(value));

            if ((underlyingType == typeof(uint)))
                return Enum.ToObject(enumType, ChangeType<uint>(value));

            if (underlyingType == typeof(short))
                return Enum.ToObject(enumType, ChangeType<short>(value));

            if (underlyingType == typeof(ushort))
                return Enum.ToObject(enumType, ChangeType<ushort>(value));

            if (underlyingType == typeof(byte))
                return Enum.ToObject(enumType, ChangeType<byte>(value));

            if (underlyingType == typeof(sbyte))
                return Enum.ToObject(enumType, ChangeType<sbyte>(value));

            throw new ArgumentException(null, nameof(enumType));
        }

        public static object ToEnum(object obj, Enum defaultValue)
        {
            if (defaultValue == null)
                throw new ArgumentNullException(nameof(defaultValue));

            if (obj == null)
                return defaultValue;

            if (obj.GetType() == defaultValue.GetType())
                return obj;

            if (EnumTryParse(defaultValue.GetType(), obj.ToString(), out var value))
                return value;

            return defaultValue;
        }

        public static object ToEnum(string text, Type enumType)
        {
            if (enumType == null)
                throw new ArgumentNullException(nameof(enumType));

            EnumTryParse(enumType, text, out var value);
            return value;
        }

        public static Enum ToEnum(string text, Enum defaultValue)
        {
            if (defaultValue == null)
                throw new ArgumentNullException(nameof(defaultValue));

            if (EnumTryParse(defaultValue.GetType(), text, out var value))
                return (Enum)value;

            return defaultValue;
        }

        public static bool EnumTryParse(Type type, object input, out object value)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!type.IsEnum)
                throw new ArgumentException(null, nameof(type));

            if (input == null)
            {
                value = Activator.CreateInstance(type);
                return false;
            }

            var stringInput = string.Format(CultureInfo.InvariantCulture, "{0}", input);
            stringInput = stringInput.Nullify();
            if (stringInput == null)
            {
                value = Activator.CreateInstance(type);
                return false;
            }

            if (stringInput.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (ulong.TryParse(stringInput.Substring(2), NumberStyles.HexNumber, null, out var ulx))
                {
                    value = ToEnum(ulx.ToString(CultureInfo.InvariantCulture), type);
                    return true;
                }
            }

            var names = Enum.GetNames(type);
            if (names.Length == 0)
            {
                value = Activator.CreateInstance(type);
                return false;
            }

            var values = Enum.GetValues(type);
            // some enums like System.CodeDom.MemberAttributes *are* flags but are not declared with Flags...
            if (!type.IsDefined(typeof(FlagsAttribute), true) && stringInput.IndexOfAny(_enumSeparators) < 0)
                return StringToEnum(type, names, values, stringInput, out value);

            // multi value enum
            var tokens = stringInput.Split(_enumSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                value = Activator.CreateInstance(type);
                return false;
            }

            ulong ul = 0;
            foreach (var tok in tokens)
            {
                var token = tok.Nullify(); // NOTE: we don't consider empty tokens as errors
                if (token == null)
                    continue;

                if (!StringToEnum(type, names, values, token, out var tokenValue))
                {
                    value = Activator.CreateInstance(type);
                    return false;
                }

                ulong tokenUl;
                switch (Convert.GetTypeCode(tokenValue))
                {
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                    case TypeCode.SByte:
                        tokenUl = (ulong)Convert.ToInt64(tokenValue, CultureInfo.InvariantCulture);
                        break;

                    default:
                        tokenUl = Convert.ToUInt64(tokenValue, CultureInfo.InvariantCulture);
                        break;
                }

                ul |= tokenUl;
            }
            value = Enum.ToObject(type, ul);
            return true;
        }

        public static T GetValue<T>(this IDictionary<string, object> dictionary, string key, T defaultValue)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (dictionary == null)
                return defaultValue;

            if (!dictionary.TryGetValue(key, out var o))
                return defaultValue;

            return ChangeType(o, defaultValue);
        }

        public static T GetValue<T>(this IDictionary<string, object> dictionary, string key, T defaultValue, IFormatProvider provider)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (dictionary == null)
                return defaultValue;

            if (!dictionary.TryGetValue(key, out var o))
                return defaultValue;

            return ChangeType(o, defaultValue, provider);
        }

        public static string GetNullifiedValue(this IDictionary<string, string> dictionary, string key) => GetNullifiedValue(dictionary, key, null);
        public static string GetNullifiedValue(this IDictionary<string, string> dictionary, string key, string defaultValue)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (dictionary == null)
                return defaultValue;

            if (!dictionary.TryGetValue(key, out var str))
                return defaultValue;

            return str.Nullify();
        }

        public static T GetValue<T>(this IDictionary<string, string> dictionary, string key, T defaultValue)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (dictionary == null)
                return defaultValue;

            if (!dictionary.TryGetValue(key, out var str))
                return defaultValue;

            return ChangeType(str, defaultValue);
        }

        public static T GetValue<T>(this IDictionary<string, string> dictionary, string key, T defaultValue, IFormatProvider provider)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (dictionary == null)
                return defaultValue;

            if (!dictionary.TryGetValue(key, out var str))
                return defaultValue;

            return ChangeType(str, defaultValue, provider);
        }

        public static bool Compare<TKey, TValue>(this IDictionary<TKey, TValue> dic1, IDictionary<TKey, TValue> dic2) => Compare(dic1, dic2, null);
        public static bool Compare<TKey, TValue>(this IDictionary<TKey, TValue> dic1, IDictionary<TKey, TValue> dic2, IEqualityComparer<TValue> comparer)
        {
            if (dic1 == null)
                return dic2 == null;

            if (dic2 == null)
                return false;

            if (dic1.Count != dic2.Count)
                return false;

            if (comparer == null)
            {
                comparer = EqualityComparer<TValue>.Default;
            }

            foreach (var kv1 in dic1)
            {
                if (!dic2.TryGetValue(kv1.Key, out var s2) || !comparer.Equals(s2, kv1.Value))
                    return false;
            }

            foreach (var kv2 in dic2)
            {
                if (!dic1.TryGetValue(kv2.Key, out var s1) || !comparer.Equals(s1, kv2.Value))
                    return false;
            }
            return true;
        }

        public static string ToStringTable<T>(this IEnumerable<T> enumerable) => ToStringTable(enumerable, (Func<T, IEnumerable<PropertyDescriptor>>)null, null, null);
        public static string ToStringTable<T>(this IEnumerable<T> enumerable, params string[] propertyNames)
        {
            if (propertyNames == null || propertyNames == null)
                return ToStringTable(enumerable);

            return ToStringTable(enumerable, null, (p) => propertyNames.Contains(p.Name, StringComparer.Ordinal), null);
        }

        public static string ToStringTable<T>(this IEnumerable<T> enumerable, Func<PropertyDescriptor, T, string> toStringFunc, params string[] propertyNames)
        {
            if (propertyNames == null || propertyNames == null)
                return ToStringTable(enumerable);

            return ToStringTable(enumerable, null, (p) => propertyNames.Contains(p.Name, StringComparer.Ordinal), toStringFunc);
        }

        public static string ToStringTable<T>(this IEnumerable<T> enumerable,
            Func<T, IEnumerable<PropertyDescriptor>> propertiesFunc,
            Func<PropertyDescriptor, bool> filterFunc,
            Func<PropertyDescriptor, T, string> toStringFunc)
        {
            if (enumerable == null)
                throw new ArgumentNullException(nameof(enumerable));

            if (propertiesFunc == null)
            {
                propertiesFunc = (o) =>
                {
                    return TypeDescriptor.GetProperties(o).OfType<PropertyDescriptor>()
                    .Where(p => p.Attributes.OfType<BrowsableAttribute>().FirstOrDefault() == null || p.Attributes.OfType<BrowsableAttribute>().First().Browsable);
                };
            }

            if (filterFunc == null)
            {
                filterFunc = (p) => true;
            }

            if (toStringFunc == null)
            {
                toStringFunc = (p, o) =>
                {
                    const int max = 50;
                    var value = p.GetValue(o);
                    if (value is string s)
                        return s;

                    if (value is byte[] bytes)
                    {
                        if (bytes.Length > (max - 1) / 2)
                            return "0x" + BitConverter.ToString(bytes, 0, (max - 1) / 2).Replace("-", string.Empty) + "... (" + bytes.Length + ")";

                        return "0x" + BitConverter.ToString(bytes, 0, Math.Min((max - 1) / 2, bytes.Length)).Replace("-", string.Empty);
                    }

                    s = string.Format("{0}", value);
                    return s.Length < max ? s : s.Substring(0, max) + "...";
                };
            }

            var first = enumerable.FirstOrDefault();
            if (first == null)
                return null;

            var properties = propertiesFunc(first).Where(filterFunc).ToArray();
            if (properties.Length == 0)
                return null;

            var columnLengths = properties.Select(p => p.Name.Length).ToArray();
            var rows = new List<string[]>();
            foreach (var row in enumerable)
            {
                var rowValues = new string[properties.Length];
                for (var i = 0; i < properties.Length; i++)
                {
                    rowValues[i] = toStringFunc(properties[i], row);
                    if (rowValues[i].Length > columnLengths[i])
                    {
                        columnLengths[i] = rowValues[i].Length;
                    }
                }
                rows.Add(rowValues);
            }

            var fullLine = new string('-', columnLengths.Sum() + 1 + columnLengths.Length * (2 + 1));
            var gridLine = new StringBuilder();
            var sb = new StringBuilder(fullLine);
            sb.AppendLine();
            sb.Append('|');
            gridLine.Append('|');
            for (var i = 0; i < properties.Length; i++)
            {
                sb.AppendFormat(CultureInfo.CurrentCulture, " {0," + columnLengths[i] + "} |", properties[i].Name);
                gridLine.Append(new string('-', columnLengths[i] + 2) + '|');
            }
            sb.AppendLine();
            sb.AppendLine(gridLine.ToString());
            for (var r = 0; r < rows.Count; r++)
            {
                var rowValues = rows[r];
                sb.Append('|');
                for (var i = 0; i < properties.Length; i++)
                {
                    sb.AppendFormat(CultureInfo.CurrentCulture, " {0," + columnLengths[i] + "} |", rowValues[i]);
                }
                sb.AppendLine();
            }
            sb.Append(fullLine);
            return sb.ToString();
        }
    }
}
