using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ModelHome.Tests
{
    public class HouseImportContractTests
    {
        public const string HousePath = "Assets/ModelHome/Models/House/ModelHome_House.fbx";

        static readonly string[] Required =
        {
            "Anchor_PlayerSpawn", "Anchor_Dad_Armchair", "Anchor_Dad_LivingDoorway", "Anchor_Mom_Counter",
            "Anchor_Mom_FacingHall", "Anchor_Mom_BackHallDoor", "Anchor_Daughter_Chair", "Anchor_Baby_Bassinet",
            "Light_FoyerLamp", "Light_LivingLamp", "Light_StudyLamp", "Light_KitchenPendant",
            "INT_Clipboard", "INT_FrontDoor", "INT_BackDoor", "INT_StudyWindow", "INT_KitchenWindow",
            "INT_UtilityWindow", "INT_LivingLamp", "INT_StudyLamp", "INT_DiningTable",
            "SND_Fridge", "SND_Clock", "COL_StairsBlock", "Rug",
        };

        static GameObject House() => AssetDatabase.LoadAssetAtPath<GameObject>(HousePath);
        static Transform Find(string n) => House().GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);

        [Test] public void ContainsAllContractNames()
        {
            Assert.IsNotNull(House());
            foreach (var n in Required) Assert.IsNotNull(Find(n), n);
        }

        [Test] public void AnchorFacing_MatchesPlan()
        {
            // Blender direction (x, y) maps to Unity (-x, -y) on the XZ plane (see Facing)
            Assert.Greater(Vector3.Dot(Facing.Of(Find("Anchor_Mom_Counter")), Vector3.back), 0.99f);     // Blender +Y
            Assert.Greater(Vector3.Dot(Facing.Of(Find("Anchor_Mom_FacingHall")), Vector3.right), 0.99f); // Blender -X
            Assert.Greater(Vector3.Dot(Facing.Of(Find("Anchor_Daughter_Chair")), Vector3.forward), 0.99f); // Blender -Y
        }

        [Test] public void HasMeshColliders() =>
            Assert.Greater(House().GetComponentsInChildren<MeshCollider>(true).Length, 0);

        [Test] public void AnchorsSitOnTheFloor_YUp()
        {
            Assert.AreEqual(0f, Find("Anchor_PlayerSpawn").position.y, 0.01f);
            Assert.AreEqual(0f, Find("Anchor_Dad_Armchair").position.y, 0.01f);
            Assert.AreEqual(0.55f, Find("Anchor_Baby_Bassinet").position.y, 0.01f);
            // plan distance spawn -> armchair: hypot(7.3-1.8, 3.8-2.4) = 5.675 m
            Assert.AreEqual(5.675f, Vector3.Distance(Find("Anchor_PlayerSpawn").position, Find("Anchor_Dad_Armchair").position), 0.02f);
        }

        [Test] public void SpawnFacesIntoTheLivingRoom()
        {
            var spawn = Find("Anchor_PlayerSpawn");
            var toLiving = (Find("Anchor_Dad_Armchair").position - spawn.position).normalized;
            Assert.Greater(Vector3.Dot(Facing.Of(spawn), toLiving), 0.5f, $"spawn facing {Facing.Of(spawn)}");
        }
    }
}
