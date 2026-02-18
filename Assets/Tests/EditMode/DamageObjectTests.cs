using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class DamageObjectTests
    {
        private GameObject damageObject;
        private DamageObject damageComponent;
        
        [SetUp]
        public void SetUp()
        {
            damageObject = new GameObject("TestDamageObject");
            damageComponent = damageObject.AddComponent<DamageObject>();
        }
        
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(damageObject);
        }
        
        [Test]
        public void DamageObject_Initialization()
        {
            Assert.IsNotNull(damageComponent);
            Assert.AreEqual("TestDamageObject", damageObject.name);
        }
        
        [Test]
        public void DamageObject_ComponentSetup()
        {
            Assert.IsNotNull(damageComponent.GetComponent<DamageObject>());
            Assert.AreEqual(typeof(DamageObject), damageComponent.GetType());
        }
        
        [Test]
        public void DamageObject_ColliderRequirement()
        {
            var collider = damageObject.AddComponent<BoxCollider2D>();
            Assert.IsNotNull(damageComponent.GetComponent<Collider2D>());
        }
        
        [Test]
        public void DamageObject_TriggerSetup()
        {
            var collider = damageObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            Assert.IsTrue(collider.isTrigger);
        }
        
        [Test]
        public void DamageObject_MultipleInstances()
        {
            var damage2 = new GameObject("TestDamage2");
            var component2 = damage2.AddComponent<DamageObject>();
            
            Assert.AreNotSame(damageComponent, component2);
            Assert.AreNotEqual(damageObject.name, damage2.name);
            
            Object.DestroyImmediate(damage2);
        }
        
        [Test]
        public void DamageObject_PositionAccuracy()
        {
            Vector3 targetPos = new Vector3(2.5f, 1.8f, 0f);
            damageObject.transform.position = targetPos;
            
            Assert.AreEqual(targetPos, damageObject.transform.position);
        }
        
        [Test]
        public void DamageObject_LayerManagement()
        {
            damageObject.layer = LayerMask.NameToLayer("Default");
            Assert.AreEqual(LayerMask.NameToLayer("Default"), damageObject.layer);
        }
        
        [Test]
        public void DamageObject_TagAssignment()
        {
            damageObject.tag = "Damage";
            Assert.AreEqual("Damage", damageObject.tag);
        }
        
        [Test]
        public void DamageObject_StateManagement()
        {
            Assert.IsTrue(damageObject.activeInHierarchy);
            
            damageObject.SetActive(false);
            Assert.IsFalse(damageObject.activeInHierarchy);
            
            damageObject.SetActive(true);
            Assert.IsTrue(damageObject.activeInHierarchy);
        }
        
        [Test]
        public void DamageObject_ComponentCombination()
        {
            damageObject.AddComponent<Rigidbody2D>();
            damageObject.AddComponent<SpriteRenderer>();
            
            Assert.IsNotNull(damageComponent.GetComponent<Rigidbody2D>());
            Assert.IsNotNull(damageComponent.GetComponent<SpriteRenderer>());
        }
    }
}