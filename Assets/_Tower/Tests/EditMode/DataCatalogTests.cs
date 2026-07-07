using System.Collections.Generic;
using NUnit.Framework;
using Tower.Core;
using Tower.Data;

namespace Tower.Tests.EditMode
{
    // Load-time validation gate for the static-data pipeline (T-Data).
    // Uses the sheetName->text Load overload so tests run without TextAssets.
    public sealed class DataCatalogTests
    {
        private const string MarksCsv =
            "id,displayName,durationTurns,stackable\n" +
            "M_Frost,Frost,2,true\n" +
            "M_Burn,Burn,3,true\n";

        private const string PassivesCsv =
            "id,displayName,effectHookKey\n" +
            "P_Reckless,Reckless,passive.reckless\n" +
            "P_Guardian,Guardian,passive.guardian\n" +
            "P_Tempo,Tempo,passive.tempo\n";

        private const string AbilitiesCsv =
            "id,displayName,tag,targetMark,range,cost,basePower,amplificationMultiplier,targetType,cooldownRounds\n" +
            "A_FrostBolt,Frost Bolt,Apply,M_Frost,5,1,6,1,Enemy,0\n" +
            "A_BurningBrand,Burning Brand,Apply,M_Burn,4,1,5,1,Enemy,0\n" +
            "A_ChillTrap,Chill Trap,Apply,M_Frost,3,2,4,1,Cell,0\n" +
            "A_ShatterFrost,Shatter Frost,Consume,M_Frost,4,2,12,1,Enemy,2\n" +
            "A_IgniteAsh,Ignite Ash,Consume,M_Burn,4,2,11,1,Enemy,0\n" +
            "A_ThermalBreak,Thermal Break,Consume,M_Burn,1,2,13,1,Enemy,2\n" +
            "A_FocusStrike,Focus Strike,Amplify,,1,1,8,1.5,Enemy,1\n" +
            "A_GuardedSurge,Guarded Surge,Amplify,,2,1,3,1.35,Ally,0\n" +
            "A_QuickSlash,Quick Slash,None,,1,0,7,1,Enemy,0\n" +
            "A_HoldLine,Hold Line,None,,1,0,2,1,Ally,0\n";

        private const string CharactersCsv =
            "id,displayName,maxHp,attack,defense,speed,disposition,passive,defaultAbilities,isReturner,chainLocked,isPreset,factionId\n" +
            "C_Returner,Returner,34,8,5,11,Protective,P_Tempo,A_QuickSlash;A_FrostBolt,true,false,false,0\n" +
            "C_EmberVanguard,Ember Vanguard,38,10,6,8,Aggressive,P_Reckless,A_BurningBrand;A_ThermalBreak,false,false,false,0\n" +
            "C_GlassBreaker,Glass Breaker,28,12,3,12,Aggressive,P_Tempo,A_FocusStrike;A_ShatterFrost,false,false,false,0\n" +
            "C_WardBearer,Ward Bearer,44,6,9,7,Protective,P_Guardian,A_HoldLine;A_GuardedSurge,false,false,false,0\n";

        private const string ItemsCsv =
            "id,displayName,resourceScope,power,stackMax,description\n" +
            "I_Poultice,Poultice,Temporary,10,3,heal a little\n" +
            "I_ShortcutKey,Shortcut Key,Permanent,0,1,shortcut token\n";

        private const string DropTablesCsv =
            "tableId,entryId,weight,rewardType,refId,minDepth,maxDepth\n" +
            "DT_FloorReward,heal_small,40,Heal,,0,\n" +
            "DT_FloorReward,resource,35,Resource,,0,\n" +
            "DT_FloorReward,ability,15,Ability,,2,\n" +
            "DT_FloorReward,shortcut,10,Shortcut,,3,\n";

        private static Dictionary<string, string> ValidSheets()
        {
            return new Dictionary<string, string>
            {
                { DataCatalog.MarksSheet, MarksCsv },
                { DataCatalog.PassivesSheet, PassivesCsv },
                { DataCatalog.AbilitiesSheet, AbilitiesCsv },
                { DataCatalog.CharactersSheet, CharactersCsv },
                { DataCatalog.ItemsSheet, ItemsCsv },
                { DataCatalog.DropTablesSheet, DropTablesCsv },
            };
        }

        [Test]
        public void ValidData_Loads_WithExpectedCounts()
        {
            var catalog = DataCatalog.Load(ValidSheets());

            Assert.AreEqual(2, catalog.Marks.Count, "Marks count");
            Assert.AreEqual(3, catalog.Passives.Count, "Passives count");
            Assert.AreEqual(10, catalog.Abilities.Count, "Abilities count");
            Assert.AreEqual(4, catalog.Characters.Count, "Characters count");
            Assert.AreEqual(2, catalog.Items.Count, "Items count");
            Assert.AreEqual(1, catalog.DropTables.Count, "DropTable groups");
            Assert.AreEqual(4, catalog.GetDropTable("DT_FloorReward").Count, "DropTable entries");
        }

        [Test]
        public void ValidData_ParsesTypedFields()
        {
            var catalog = DataCatalog.Load(ValidSheets());

            var frost = catalog.GetMark("M_Frost");
            Assert.IsNotNull(frost);
            Assert.AreEqual(2, frost.DurationTurns);
            Assert.IsTrue(frost.Stackable);

            var focus = catalog.GetAbility("A_FocusStrike");
            Assert.IsNotNull(focus);
            Assert.AreEqual(AbilityTag.Amplify, focus.Tag);
            Assert.AreEqual(AbilityTargetType.Enemy, focus.TargetType);
            Assert.AreEqual(1.5f, focus.AmplificationMultiplier, 0.0001f);
            Assert.AreEqual(string.Empty, focus.TargetMark, "Amplify has empty targetMark");

            var returner = catalog.GetCharacter("C_Returner");
            Assert.IsNotNull(returner);
            Assert.AreEqual(DispositionType.Protective, returner.Disposition);
            Assert.IsTrue(returner.IsReturner);
            Assert.AreEqual(2, returner.DefaultAbilities.Count);
            Assert.AreEqual("A_QuickSlash", returner.DefaultAbilities[0]);
            Assert.AreEqual("A_FrostBolt", returner.DefaultAbilities[1]);

            var poultice = catalog.GetItem("I_Poultice");
            Assert.IsNotNull(poultice);
            Assert.AreEqual(ResourceScope.Temporary, poultice.ResourceScope);

            var entries = catalog.GetDropTable("DT_FloorReward");
            Assert.AreEqual(RewardType.Heal, entries[0].RewardType);
            Assert.AreEqual(int.MaxValue, entries[0].MaxDepth, "blank maxDepth is open-ended");
        }

        [Test]
        public void BadEnum_IsCaught_ByValidation()
        {
            var sheets = ValidSheets();
            sheets[DataCatalog.AbilitiesSheet] =
                "id,displayName,tag,targetMark,range,cost,basePower,amplificationMultiplier,targetType,cooldownRounds\n" +
                "A_FrostBolt,Frost Bolt,Freeze,M_Frost,5,1,6,1,Enemy,0\n"; // Freeze is not an AbilityTag

            var ex = Assert.Throws<DataValidationException>(() => DataCatalog.Load(sheets));
            StringAssert.Contains("Abilities", ex.Message);
            StringAssert.Contains("tag", ex.Message);
            StringAssert.Contains("Freeze", ex.Message);
        }

        [Test]
        public void EmptyRequired_IsCaught_ByValidation()
        {
            var sheets = ValidSheets();
            sheets[DataCatalog.MarksSheet] =
                "id,displayName,durationTurns,stackable\n" +
                ",Frost,2,true\n"; // empty required id

            var ex = Assert.Throws<DataValidationException>(() => DataCatalog.Load(sheets));
            StringAssert.Contains("Marks", ex.Message);
            StringAssert.Contains("id", ex.Message);
        }

        [Test]
        public void BrokenForeignKey_IsCaught_ByValidation()
        {
            var sheets = ValidSheets();
            // Character references an ability id that does not exist.
            sheets[DataCatalog.CharactersSheet] =
                "id,displayName,maxHp,attack,defense,speed,disposition,passive,defaultAbilities,isReturner,chainLocked,isPreset,factionId\n" +
                "C_Returner,Returner,34,8,5,11,Protective,P_Tempo,A_QuickSlash;A_DoesNotExist,true,false,false,0\n";

            var ex = Assert.Throws<DataValidationException>(() => DataCatalog.Load(sheets));
            StringAssert.Contains("Characters", ex.Message);
            StringAssert.Contains("defaultAbilities", ex.Message);
            StringAssert.Contains("A_DoesNotExist", ex.Message);
        }

        [Test]
        public void BrokenAbilityTargetMarkForeignKey_IsCaught()
        {
            var sheets = ValidSheets();
            sheets[DataCatalog.AbilitiesSheet] =
                "id,displayName,tag,targetMark,range,cost,basePower,amplificationMultiplier,targetType,cooldownRounds\n" +
                "A_FrostBolt,Frost Bolt,Apply,M_Ghost,5,1,6,1,Enemy,0\n"; // M_Ghost not a Mark

            var ex = Assert.Throws<DataValidationException>(() => DataCatalog.Load(sheets));
            StringAssert.Contains("Abilities", ex.Message);
            StringAssert.Contains("targetMark", ex.Message);
            StringAssert.Contains("M_Ghost", ex.Message);
        }

        [Test]
        public void BadInt_IsCaught_ByValidation()
        {
            var sheets = ValidSheets();
            sheets[DataCatalog.MarksSheet] =
                "id,displayName,durationTurns,stackable\n" +
                "M_Frost,Frost,two,true\n"; // durationTurns not an int

            var ex = Assert.Throws<DataValidationException>(() => DataCatalog.Load(sheets));
            StringAssert.Contains("Marks", ex.Message);
            StringAssert.Contains("durationTurns", ex.Message);
        }

        [Test]
        public void DuplicateId_IsCaught_ByValidation()
        {
            var sheets = ValidSheets();
            sheets[DataCatalog.MarksSheet] =
                "id,displayName,durationTurns,stackable\n" +
                "M_Frost,Frost,2,true\n" +
                "M_Frost,FrostDup,3,false\n"; // duplicate id

            var ex = Assert.Throws<DataValidationException>(() => DataCatalog.Load(sheets));
            StringAssert.Contains("Marks", ex.Message);
            StringAssert.Contains("duplicate", ex.Message);
        }

        [Test]
        public void MultipleViolations_AllReportedInOneError()
        {
            var sheets = ValidSheets();
            sheets[DataCatalog.MarksSheet] =
                "id,displayName,durationTurns,stackable\n" +
                ",Frost,notint,notbool\n"; // empty id + bad int + bad bool

            var ex = Assert.Throws<DataValidationException>(() => DataCatalog.Load(sheets));
            StringAssert.Contains("durationTurns", ex.Message);
            StringAssert.Contains("stackable", ex.Message);
        }
    }
}
