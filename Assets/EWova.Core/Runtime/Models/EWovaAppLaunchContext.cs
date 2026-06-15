using System;
using System.Collections.Generic;

namespace EWova
{
    /// <summary>
    /// 是否從 EWova 元宇宙應用程式啟動到或跳轉到此應用程式
    /// </summary>
    public class EWovaAppLaunchContext
    {
        public const string WorldIdKey = "wid";
        public const string SpaceIdKey = "sid";

        /// <summary>
        /// 如果有值，代表是從該課程世界來的
        /// </summary>
        public Guid? WorldGuid;
        /// <summary>
        /// 如果有值，代表是從該課程世界的空間來的
        /// </summary>
        public int? SpaceInstanceIndex;
    }
}
