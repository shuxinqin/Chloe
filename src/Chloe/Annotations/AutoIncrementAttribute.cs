﻿namespace Chloe.Annotations
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class AutoIncrementAttribute : Attribute
    {
    }
}
