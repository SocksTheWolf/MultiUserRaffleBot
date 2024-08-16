using System;
using System.Reflection;

namespace MultiUserRaffleBot.Utils
{
    public static class EnumExtension
    {
        // Enum extension helper
        public static TAttribute? GetEnumAttribute<TEnum, TAttribute>(this TEnum Enum)
            where TEnum : Enum
            where TAttribute : Attribute
        {
            var MemberInfo = typeof(TEnum).GetField(Enum.ToString());
            return MemberInfo?.GetCustomAttribute<TAttribute>();
        }
    }
}
