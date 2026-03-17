using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class WeaponClassControllerTests
    {
        private GameObject weaponObject;
        private WeaponClassController weaponController;
        private GameObject playerObject;
        
        [SetUp]
        public void SetUp()
        {
            // Create weapon object
            weaponObject = new GameObject("TestWeapon");
            weaponController = weaponObject.AddComponent<WeaponClassController>();
            
            // Create player object
            playerObject = new GameObject("TestPlayer");
            playerObject.AddComponent<PlayerMovement>();
        }
        
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(weaponObject);
            Object.DestroyImmediate(playerObject);
        }
        
        [Test]
        public void WeaponClassController_Initialization()
        {
            Assert.IsNotNull(weaponController);
            Assert.AreEqual("TestWeapon", weaponObject.name);
        }
        
        [Test]
        public void WeaponClassController_ComponentSetup()
        {
            Assert.IsNotNull(weaponController.GetComponent<WeaponClassController>());
            Assert.AreEqual(typeof(WeaponClassController), weaponController.GetType());
        }
        
        [Test]
        public void WeaponClassController_TransformProperties()
        {
            var transform = weaponController.transform;
            Assert.IsNotNull(transform);
            
            // Test position manipulation
            transform.position = Vector3.right;
            Assert.AreEqual(Vector3.right, transform.position);
            
            // Test rotation
            transform.rotation = Quaternion.identity;
            Assert.AreEqual(Quaternion.identity, transform.rotation);
        }
        
        [Test]
        public void WeaponClassController_GameObjectManagement()
        {
            Assert.IsTrue(weaponObject.activeInHierarchy);
            
            // Test activation/deactivation
            weaponObject.SetActive(false);
            Assert.IsFalse(weaponObject.activeInHierarchy);
            
            weaponObject.SetActive(true);
            Assert.IsTrue(weaponObject.activeInHierarchy);
        }
        
        [Test]
        public void WeaponClassController_MultipleWeapons()
        {
            // Test creating multiple weapons
            var weapon2 = new GameObject("TestWeapon2");
            var controller2 = weapon2.AddComponent<WeaponClassController>();
            
            Assert.AreNotSame(weaponController, controller2);
            Assert.AreNotEqual(weaponObject.name, weapon2.name);
            
            Object.DestroyImmediate(weapon2);
        }
        
        [Test]
        public void WeaponClassController_PlayerInteraction()
        {
            // Test that player and weapon can coexist
            Assert.IsNotNull(Object.FindFirstObjectByType<PlayerMovement>());
            Assert.IsNotNull(Object.FindFirstObjectByType<WeaponClassController>());
        }
        
        [Test]
        public void WeaponClassController_ComponentAddition()
        {
            // Test adding additional components to weapon
            var collider = weaponObject.AddComponent<BoxCollider2D>();
            Assert.IsNotNull(weaponController.GetComponent<BoxCollider2D>());
            
            var rigidbody = weaponObject.AddComponent<Rigidbody2D>();
            Assert.IsNotNull(weaponController.GetComponent<Rigidbody2D>());
        }
        
        [Test]
        public void WeaponClassController_HierarchyManagement()
        {
            // Test parent-child relationships
            var weaponHolder = new GameObject("WeaponHolder");
            weaponObject.transform.SetParent(weaponHolder.transform);
            
            Assert.AreEqual(weaponHolder.transform, weaponObject.transform.parent);
            Assert.IsTrue(weaponHolder.transform.childCount > 0);
            
            Object.DestroyImmediate(weaponHolder);
        }
        
        [Test]
        public void WeaponClassController_PositionalAccuracy()
        {
            // Test precise positioning
            Vector3 targetPosition = new Vector3(1.5f, 2.7f, 0f);
            weaponObject.transform.position = targetPosition;
            
            Assert.AreEqual(targetPosition.x, weaponObject.transform.position.x, 0.001f);
            Assert.AreEqual(targetPosition.y, weaponObject.transform.position.y, 0.001f);
        }
        
        [Test]
        public void WeaponClassController_TagAndLayerManagement()
        {
            // Test tag assignment
            weaponObject.tag = "Weapon";
            Assert.AreEqual("Weapon", weaponObject.tag);
            
            // Test layer assignment  
            weaponObject.layer = LayerMask.NameToLayer("Default");
            Assert.AreEqual(LayerMask.NameToLayer("Default"), weaponObject.layer);
        }
    }
}