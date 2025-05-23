﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Serializers.Json
{
    using Furly.Extensions.Serializers;
    using Furly.Extensions.Serializers.MessagePack;
    using Furly.Extensions.Serializers.Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using Xunit;
    using Xunit.Categories;

    [UnitTest]
    public class DefaultJsonInteropTests
    {
        public static ISerializer Json => new DefaultJsonSerializer();
        public static ISerializer MsgPack => new MessagePackSerializer();

        public static IEnumerable<(VariantValue, object)> GetStrings()
        {
            yield return ("", "");
            yield return ("str ing", "str ing");
            yield return ("{}", "{}");
            yield return (Array.Empty<byte>(), Array.Empty<byte>());
            yield return (new byte[1000], new byte[1000]);
            yield return (new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            yield return (Encoding.UTF8.GetBytes("utf-8-string"), Encoding.UTF8.GetBytes("utf-8-string"));
        }

        public static IEnumerable<(VariantValue, object?)> GetValues()
        {
            yield return (true, true);
            yield return (false, false);
            yield return ((bool?)null, null);
            yield return ((sbyte)1, (sbyte)1);
            yield return ((sbyte)-1, (sbyte)-1);
            yield return ((sbyte)0, (sbyte)0);
            yield return (sbyte.MaxValue, sbyte.MaxValue);
            yield return (sbyte.MinValue, sbyte.MinValue);
            yield return ((sbyte?)null, null);
            yield return ((short)1, (short)1);
            yield return ((short)-1, (short)-1);
            yield return ((short)0, (short)0);
            yield return (short.MaxValue, short.MaxValue);
            yield return (short.MinValue, short.MinValue);
            yield return ((short?)null, null);
            yield return (1, 1);
            yield return (-1, -1);
            yield return (0, 0);
            yield return (int.MaxValue, int.MaxValue);
            yield return (int.MinValue, int.MinValue);
            yield return ((int?)null, null);
            yield return (1L, 1L);
            yield return (-1L, -1L);
            yield return (0L, 0L);
            yield return (long.MaxValue, long.MaxValue);
            yield return (long.MinValue, long.MinValue);
            yield return ((long?)null, null);
            yield return (1UL, 1UL);
            yield return (0UL, 0UL);
            yield return (11790719998462990154, 11790719998462990154);
            yield return (10042278942021613161, 10042278942021613161);
            yield return (ulong.MaxValue, ulong.MaxValue);
            yield return ((ulong?)null, null);
            yield return (1u, 1u);
            yield return (0u, 0u);
            yield return (uint.MaxValue, uint.MaxValue);
            yield return ((uint?)null, null);
            yield return ((ushort)1, (ushort)1);
            yield return ((ushort)0, (ushort)0);
            yield return (ushort.MaxValue, ushort.MaxValue);
            yield return ((ushort?)null, null);
            yield return ((byte)1, (byte)1);
            yield return ((byte)0, (byte)0);
            yield return (1.0, 1.0);
            yield return (-1.0, -1.0);
            yield return (0.0, 0.0);
            yield return (byte.MaxValue, byte.MaxValue);
            yield return ((byte?)null, null);
            yield return (double.MaxValue, double.MaxValue);
            yield return (double.MinValue, double.MinValue);
            yield return (double.PositiveInfinity, double.PositiveInfinity);
            yield return (double.NegativeInfinity, double.NegativeInfinity);
            yield return ((double?)null, null);
            yield return (1.0f, 1.0f);
            yield return (-1.0f, -1.0f);
            yield return (0.0f, 0.0f);
            yield return (float.MaxValue, float.MaxValue);
            yield return (float.MinValue, float.MinValue);
            yield return (float.PositiveInfinity, float.PositiveInfinity);
            yield return (float.NegativeInfinity, float.NegativeInfinity);
            yield return ((float?)null, null);
            yield return ((decimal)1.0, (decimal)1.0);
            yield return ((decimal)-1.0, (decimal)-1.0);
            yield return ((decimal)0.0, (decimal)0.0);
            yield return ((decimal)1234567, (decimal)1234567);
            yield return ((decimal?)null, null);
            //  yield return (decimal.MaxValue, decimal.MaxValue);
            //  yield return (decimal.MinValue, decimal.MinValue);
            var guid = Guid.NewGuid();
            yield return (guid, guid);
            yield return (Guid.Empty, Guid.Empty);
            var now1 = DateTime.UtcNow;
            yield return (now1, now1);
            yield return (DateTime.MinValue, DateTime.MinValue);
            yield return (DateTime.MaxValue, DateTime.MaxValue); // Works only in 2.0
            yield return ((DateTime?)null, null);
            var now2 = DateTimeOffset.UtcNow;
            yield return (now2, now2);
            // TODO FIX yield return (DateTimeOffset.MaxValue, DateTimeOffset.MaxValue);
            // TODO FIX yield return (DateTimeOffset.MinValue, DateTimeOffset.MinValue);
            yield return ((DateTimeOffset?)null, null);
            yield return (TimeSpan.Zero, TimeSpan.Zero);
            yield return (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            yield return (TimeSpan.FromDays(5555), TimeSpan.FromDays(5555));
            yield return (TimeSpan.MaxValue, TimeSpan.MaxValue);
            yield return (TimeSpan.MinValue, TimeSpan.MinValue);
            yield return ((TimeSpan?)null, null);
        }

        [Fact]
        public void SerializerXmlElement()
        {
            var document = new XmlDocument();
            document.LoadXml(@"<note>
<to>Tove</to>
<from>Jani</from>
<heading>Reminder</heading>
<body>Don't forget me this weekend!</body>
</note>");
            var expected = document.DocumentElement;
            var result = Json.FromObject(expected);
            var actual = MsgPack.Parse(MsgPack.SerializeToString(expected));

            Assert.Equal(result, actual);
        }

        [Fact]
        public void SerializerArrayOfArraysVariant()
        {
            var o = new int[] { 0, 0, 0, 0 };
            var expected = Json.FromArray(o, o, o);
            var str = MsgPack.SerializeToString(expected);
            var result = MsgPack.Parse(str);
            Assert.True(result.IsArray);
            Assert.Equal(3, result.Count);
            Assert.Equal(expected.GetTypeCode(), result.GetTypeCode());
            Assert.Equal(expected.Count, result.Count);
            Assert.Equal(expected, result);
        }

        [Theory]
        [MemberData(nameof(GetScalars))]
        [MemberData(nameof(GetEmptyArrays))]
        [MemberData(nameof(GetFilledArrays))]
        public void SerializerArrayVariant2(object o, Type type)
        {
            Assert.NotNull(type);
            var expected = Json.FromArray(o, o);
            var result = MsgPack.Parse(MsgPack.SerializeToString(expected));
            Assert.True(result.IsArray);
            Assert.Equal(2, result.Count);
            Assert.Equal(expected.GetTypeCode(), result.GetTypeCode());
            Assert.Equal(expected.Count, result.Count);
            Assert.Equal(expected, result);
        }

        [Theory]
        [MemberData(nameof(GetScalars))]
        [MemberData(nameof(GetEmptyArrays))]
        [MemberData(nameof(GetFilledArrays))]
        public void SerializerArrayVariant3(object o, Type type)
        {
            Assert.NotNull(type);
            var expected = Json.FromArray(o, o, o);
            var result = MsgPack.Parse(MsgPack.SerializeToString(expected));
            Assert.True(result.IsArray);
            Assert.Equal(3, result.Count);
            Assert.Equal(expected.GetTypeCode(), result.GetTypeCode());
            Assert.Equal(expected.Count, result.Count);
            Assert.Equal(expected, result);
        }

        [Theory]
        [MemberData(nameof(GetScalars))]
        [MemberData(nameof(GetEmptyArrays))]
        [MemberData(nameof(GetFilledArrays))]
        public void SerializerArrayVariantToObject(object o, Type type)
        {
            Assert.NotNull(type);
            var expected = Json.FromArray(o, o, o);
            var result = MsgPack.Parse(MsgPack.SerializeToString(expected.ConvertTo(type.MakeArrayType())));

            Assert.True(result.IsArray);
            Assert.Equal(3, result.Count);
            Assert.Equal(expected.GetTypeCode(), result.GetTypeCode());
            Assert.Equal(expected.Count, result.Count);
            Assert.Equal(expected, result);
        }

        [Theory]
        [MemberData(nameof(GetScalars))]
        [MemberData(nameof(GetEmptyArrays))]
        [MemberData(nameof(GetFilledArrays))]
        public void SerializerVariant(object o, Type type)
        {
            var expected = Json.FromObject(o).ConvertTo(type);
            var result = MsgPack.FromObject(o).ConvertTo(type);
            Assert.NotNull(expected);
            Assert.NotNull(result);
            Assert.Equal(expected, result);
            Assert.Equal(o, result);
            Assert.Equal(o.GetType(), result?.GetType());
            Assert.Equal(o.GetType(), expected?.GetType());
        }

        [Theory]
        [MemberData(nameof(GetVariantValues))]
        public void SerializerFromObject(VariantValue v)
        {
            var expected = Json.FromObject(v);
            var result = MsgPack.FromObject(v);

            // TODO:   Assert.Equal(expected.GetTypeCode(), result.GetTypeCode());
            Assert.Equal(expected, result);
        }

        [Fact]
        public void SerializeFromObjectsWithSameContent0()
        {
            var expected = Json.FromObject(new
            {
                Test = "Text",
                Locale = "de"
            });
            var actual = MsgPack.FromObject(new
            {
                Test = "Text",
                Locale = "de"
            });
            Assert.True(expected.Equals(actual));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SerializeFromObjectsWithSameContent1()
        {
            var expected = Json.FromObject(new
            {
                Test = "Text",
                Locale = "de"
            });
            var actual = MsgPack.FromObject(new
            {
                Locale = "de",
                Test = "Text"
            });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SerializeFromObjectsWithSameContent2()
        {
            var utc = DateTime.UtcNow;
            var expected = Json.FromObject(new
            {
                Test = 1,
                LoCale = "de",
                TimeStamp = utc
            });
            var actual = MsgPack.FromObject(new
            {
                TimeStamp = utc,
                Locale = "de",
                TeSt = 1
            });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SerializeFromObjectsWithSameContent3()
        {
            var utc = DateTime.UtcNow;
            var expected = Json.FromObject(new
            {
                Test = 1,
                LoCale = "de",
                Inner = new
                {
                    TimeStamp = utc,
                    Test = 1
                }
            });
            var actual = MsgPack.FromObject(new
            {
                Locale = "de",
                Inner = new
                {
                    TimeStamp = utc,
                    Test = 1
                },
                TeSt = 1
            });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SerializeFromObjectsWithSameContent3Converted()
        {
            var utc = DateTime.UtcNow;
            var expected = Json.FromObject(new
            {
                Test = 1,
                LoCale = "de",
                Inner = new
                {
                    TimeStamp = utc,
                    Test = 1
                }
            });
            var actual = MsgPack.FromObject(expected);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SerializeFromObjectsWithSameContent4()
        {
            var utc = DateTime.UtcNow;
            var expected = Json.FromObject(new
            {
                Test = 11790719998462990154UL,
                LoCale = 10042278942021613161UL,
                TimeStamp = utc
            });
            var actual = MsgPack.FromObject(new
            {
                TimeStamp = utc,
                Locale = 10042278942021613161UL,
                TeSt = 11790719998462990154UL
            });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SerializeFromObjectsWithSameContentConverted()
        {
            var utc = DateTime.UtcNow;
            var expected = Json.FromObject(new
            {
                Test = 11790719998462990154UL,
                LoCale = 10042278942021613161UL,
                TimeStamp = utc
            });
            var actual = MsgPack.FromObject(expected);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SerializerFromObjectContainerToContainerWithObject()
        {
            var expected = new TestContainer
            {
                Value = Json.FromObject(new
                {
                    Test = "Text",
                    Locale = "de"
                })
            };
            var tmp = MsgPack.FromObject(expected);
            var actual = tmp.ConvertTo<TestContainer>();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SerializerFromJsonToObject()
        {
            var expected = Json.Parse("""

    {
        "TypeId": "http://test.org/UA/Data/#i=9440",
        "Encoding": "Json",
        "Body": {
            "BooleanValue": false,
            "SByteValue": -113,
            "ByteValue": 42,
            "Int16Value": -14982,
            "UInt16Value": 59442,
            "Int32Value": 85049805,
            "UInt32Value": 2602718263,
            "Int64Value": 3649290182186472621,
            "UInt64Value": 10042278942021613161
        }
    }

""");
            var actual = MsgPack.FromObject(expected);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SerializerFromJsonToArray()
        {
            var expected = Json.Parse("""

[
    {
        "TypeId": "http://test.org/UA/Data/#i=9440",
        "Body": {
            "UInt64Value": 10042278942021613161
        }
    },
    {
        "TypeId": "http://test.org/UA/Data/#i=9440",
        "Body": {
            "UInt64Value": 11790719998462990154
        }
    },
    {
        "TypeId": "http://test.org/UA/Data/#i=9440",
        "Body": {
            "UInt64Value": 9999999999999999999999
        }
    },
    {
        "TypeId": "http://test.org/UA/Data/#i=9440",
        "Body": {
            "UInt64Value": 11790719998462990154
        }
    }
]

""");
            var actual = MsgPack.FromObject(expected);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(GetVariantValues))]
        public void SerializerFromObjectContainerToContainer(object v)
        {
            var expected = new TestContainer
            {
                Value = Json.FromObject(v)
            };
            var tmp = MsgPack.FromObject(expected);
            var actual = tmp.ConvertTo<TestContainer>();
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(GetScalars))]
        [MemberData(nameof(GetEmptyArrays))]
        [MemberData(nameof(GetFilledArrays))]
        public void SerializerFromObjectContainerToContainerWithSerializedVariant(object o, Type type)
        {
            Assert.NotNull(type);
            var expected = new TestContainer
            {
                Value = Json.FromObject(o)
            };
            var tmp = MsgPack.FromObject(expected);
            var actual = tmp.ConvertTo<TestContainer>();
            Assert.Equal(expected, actual);
            Assert.NotNull(actual?.Value);
        }

        [Theory]
        [MemberData(nameof(GetScalars))]
        [MemberData(nameof(GetEmptyArrays))]
        [MemberData(nameof(GetFilledArrays))]
        public void SerializerFromObjectContainerToContainerWithArray(object o, Type type)
        {
            Assert.NotNull(type);
            var expected = new TestContainer
            {
                Value = Json.FromArray(o, o, o)
            };
            var tmp = MsgPack.FromObject(expected);
            var actual = tmp.ConvertTo<TestContainer>();
            Assert.Equal(expected, actual);
            Assert.NotNull(actual?.Value);
        }

        [Fact]
        public void SerializerFromObjectContainerToContainerWithStringArray()
        {
            var expected = new TestContainer
            {
                Value = Json.FromArray("", "", "")
            };
            var tmp = MsgPack.FromObject(expected);
            var actual = tmp.ConvertTo<TestContainer>();
            Assert.Equal(expected, actual);
            Assert.NotNull(actual?.Value);
        }

        public static IEnumerable<object[]> GetScalars()
        {
            return GetStrings()
                .Select(v => new object[] { v.Item2, v.Item2.GetType() })
                .Concat(GetValues()
                .Where(v => v.Item2 != null)
                .Select(v => new object[] { v.Item2!, v.Item2!.GetType() })
                .Concat(GetValues()
                .Where(v => v.Item2 != null)
                .Select(v => new object[] { v.Item2!,
                    typeof(Nullable<>).MakeGenericType(v.Item2!.GetType()) })));
        }

        public static IEnumerable<object[]> GetFilledArrays()
        {
            return GetStrings()
                .Select(v => new object[] { CreateArray(v.Item2, v.Item2.GetType(), 10),
                    v.Item2.GetType().MakeArrayType()})
                .Concat(GetValues()
                .Where(v => v.Item2 != null)
                .Select(v => new object[] { CreateArray(v.Item2, v.Item2!.GetType(), 10),
                    v.Item2.GetType().MakeArrayType() }));
        }

        public static IEnumerable<object[]> GetEmptyArrays()
        {
            return GetStrings()
                .Select(v => new object[] { CreateArray(null, v.Item2!.GetType(), 10),
                    v.Item2.GetType().MakeArrayType()})
                .Concat(GetValues()
                .Where(v => v.Item2 != null)
                .Select(v => new object[] { CreateArray(null, v.Item2!.GetType(), 10),
                    v.Item2.GetType().MakeArrayType() }));
        }

        public static IEnumerable<object[]> GetVariantValues()
        {
            return GetValues()
                .Select(v => new object[] { v.Item1 })
                .Concat(GetStrings()
                .Select(v => new object[] { v.Item1 }));
        }

        private static Array CreateArray(object? value, Type type, int size)
        {
            var array = Array.CreateInstance(type, size);
            if (value != null)
            {
                for (var i = 0; i < size; i++)
                {
                    array.SetValue(value, i);
                }
            }
            return array;
        }
    }
}
