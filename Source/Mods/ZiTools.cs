using HarmonyLib;
using Multiplayer.API;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace Multiplayer.Compat
{
    /// <summary>ZiTools objects seeker by MaxZiCode</summary>
    /// <see href="https://github.com/MaxZiCode/ZiTools"/>
    /// <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3280114640"/>
    [MpCompatFor("MaxZiCode.dws.ZiTools")]
    public class ZiTools
    {
        static object FavChange = null;
        static MethodInfo GetObjectsDatabase;
        static PropertyInfo PropFavList;

        static MethodInfo ListContainsMethod;
        static MethodInfo ListAddMethod;
        static MethodInfo ListRemoveMethod;

        static FieldInfo DontSync;

        public ZiTools(ModContentPack mod)
        {
            LongEventHandler.ExecuteWhenFinished(LatePatch);
        }
        void LatePatch()
        {
            DontSync = AccessTools.Field(AccessTools.TypeByName("Multiplayer.Client.Multiplayer"), "dontSync");

            var type = AccessTools.TypeByName("ZiTools.ZiTools_GameComponent");
            GetObjectsDatabase = AccessTools.Method(type, "GetObjectsDatabase");

            type = AccessTools.TypeByName("ZiTools.ObjectsDatabase");
            PropFavList = AccessTools.Property(type, "UnitsInFavourites");

            type = AccessTools.TypeByName("ZiTools.ObjectSeeker_Window");
            MpCompat.harmony.Patch(AccessTools.Method(type, "DoWindowContents"),
                postfix: new HarmonyMethod(typeof(ZiTools), nameof(PostDoWindowContents))
                );
            MpCompat.harmony.Patch(AccessTools.Method(type, "DrawObjectsList"),
                postfix: new HarmonyMethod(typeof(ZiTools), nameof(PostDrawObjectsList))
                );

            MP.RegisterSyncMethod(AccessTools.Method(typeof(ZiTools), nameof(ZiTools.SyncedRemoveFromFav)));
            MP.RegisterSyncMethod(AccessTools.Method(typeof(ZiTools), nameof(ZiTools.SyncedAddToFav)));

            type = AccessTools.TypeByName("ZiTools.DBUnit");
            ListContainsMethod = AccessTools.Method(typeof(List<>).MakeGenericType(type), "Contains");
            ListAddMethod = AccessTools.Method(typeof(List<>).MakeGenericType(type), "Add");
            ListRemoveMethod = AccessTools.Method(typeof(List<>).MakeGenericType(type), "Remove");

            type = AccessTools.TypeByName("ZiTools.MapMarksManager");
            MpCompat.harmony.Patch(AccessTools.Method(type, "SetMarks"),
                prefix: new HarmonyMethod(typeof(MapMarksManager_Marks_Patch), nameof(MapMarksManager_Marks_Patch.Prefix)),
                finalizer: new HarmonyMethod(typeof(MapMarksManager_Marks_Patch), nameof(MapMarksManager_Marks_Patch.Finalizer))
                );
            MpCompat.harmony.Patch(AccessTools.Method(type, "RemoveMarks"),
                prefix: new HarmonyMethod(typeof(MapMarksManager_Marks_Patch), nameof(MapMarksManager_Marks_Patch.Prefix)),
                finalizer: new HarmonyMethod(typeof(MapMarksManager_Marks_Patch), nameof(MapMarksManager_Marks_Patch.Finalizer))
                );
        }

        static class MapMarksManager_Marks_Patch
        {
            static object preDontSync;
            public static void Prefix()
            {
                preDontSync = DontSync.GetValue(null);
                DontSync.SetValue(null, true);
            }
            public static void Finalizer()
            {
                DontSync.SetValue(null, preDontSync);
            }
        }


        public static void PostDrawObjectsList(ref IExposable favChange)
        {
            if (favChange != null)
            {
                FavChange = favChange;
            }
        }
        public static void PostDoWindowContents()
        {
            if(FavChange != null)
            {
                var list = GetFavList();
                if(list == null)
                {
                    Log.Warning("ZiTools have no Favlist now");
                    FavChange = null;
                    return;
                }
                if (!(bool)ListContainsMethod.Invoke(list, [FavChange]))
                {
                    ListAddMethod.Invoke(list, [FavChange]);
                    SyncedRemoveFromFav(FavChange);
                }
                else
                {
                    ListRemoveMethod.Invoke(list, [FavChange]);
                    SyncedAddToFav(FavChange);
                }
                FavChange = null;
            }
        }
        static object GetFavList()
        {
            object odb = GetObjectsDatabase.Invoke(null, null);
            var list = PropFavList?.GetValue(odb);

            return list;
        }
        static void SyncedRemoveFromFav(object favChange)
        {
            ListRemoveMethod.Invoke(GetFavList(), [FavChange]);
        }
        static void SyncedAddToFav(object favChange)
        {
            ListAddMethod.Invoke(GetFavList(), [FavChange]);
        }
    }
}