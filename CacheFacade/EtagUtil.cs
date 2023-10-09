// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;

    public static class EtagUtil
    {
        public static string GenerateEtag()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
