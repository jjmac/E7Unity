using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;
using UnityEngine;

namespace E7.ECS
{
    /// <summary>
    /// Used for manual injection from outside of ECS.
    /// </summary>
    public class InjectorSystem : ComponentSystem
    {
        protected override void OnCreateManager(int capacity)
        {
            this.Enabled = false;
        }

        protected override void OnUpdate() { Debug.LogError("Should not ever run!"); }

        public ComponentDataArray<T> Inject<T>() where T : struct, IComponentData
        {
            var group = GetComponentGroup(ComponentType.Create<T>());
            var cda = group.GetComponentDataArray<T>();
            group.Dispose();
            return cda;
        }

        public (ComponentDataArray<T1>, ComponentDataArray<T2>) Inject<T1, T2>()
        where T1 : struct, IComponentData
        where T2 : struct, IComponentData
        {
            var group = GetComponentGroup(ComponentType.Create<T1>(), ComponentType.Create<T2>());
            var cda = group.GetComponentDataArray<T1>();
            var cda2 = group.GetComponentDataArray<T2>();
            group.Dispose();
            return (cda, cda2);
        }

    }

    /// <summary>
    /// A slew of cheat functions to help you bridge `MonoBehaviour` world with ECS.
    /// Also useful as a band-aid solution while moving to ECS.
    /// </summary>
    public static class MonoECS
    {
        private static EntityManager em => World.Active.GetExistingManager<EntityManager>();

        /// <summary>
        /// A very inefficient and barbaric way of getting a filtered entities outside of ECS world.
        /// Useful for when you are in the middle of moving things to ECS. Runs on your active world's EntityManager.
        /// </summary>
        public static (T component, Entity entity)[] Inject<T>() where T : struct, IComponentData
        {
            List<(T, Entity)> list = new List<(T, Entity)>();
            using (NativeArray<Entity> allEntities = em.GetAllEntities())
            {
                foreach (Entity e in allEntities)
                {
                    if (em.HasComponent<T>(e))
                    {
                        T componentData = em.GetComponentData<T>(e);
                        list.Add((componentData, e));
                    }
                }
            }
            return list.ToArray();
        }

        /// <summary>
        /// A very inefficient and barbaric way of getting a filtered entities outside of ECS world.
        /// Useful for when you are in the middle of moving things to ECS. Runs on your active world's EntityManager.
        /// </summary>
        public static (T1 component1, T2 component2, Entity entity)[] Inject<T1, T2>()
        where T1 : struct, IComponentData
        where T2 : struct, IComponentData
        {
            List<(T1, T2, Entity)> list = new List<(T1, T2, Entity)>();
            using (NativeArray<Entity> allEntities = em.GetAllEntities())
            {
                foreach (Entity e in allEntities)
                {
                    if (em.HasComponent<T1>(e) && em.HasComponent<T2>(e))
                    {
                        T1 componentData = em.GetComponentData<T1>(e);
                        T2 componentData2 = em.GetComponentData<T2>(e);
                        list.Add((componentData, componentData2, e));
                    }
                }
            }
            return list.ToArray();
        }

        /// <summary>
        /// Get all of your `MonoBehaviour` attached with `GameObjectEntity` outside of ECS.
        /// </summary>
        /// <returns>Contains both the component and its corresponding entity generated from `GameObjectEntity`.</returns>
        public static (T monoComponent, Entity entity)[] InjectMono<T>() where T : Component
        {
            List<(T, Entity)> list = new List<(T, Entity)>();
            using (NativeArray<Entity> allEntities = em.GetAllEntities())
            {
                foreach (Entity e in allEntities)
                {
                    if (em.HasComponent<T>(e))
                    {
                        T componentData = em.GetComponentObject<T>(e);
                        list.Add((componentData, e));
                    }
                }
            }
            return list.ToArray();
        }


        public static bool HasTag<T>(Entity entity) where T : struct, IComponentData, ITag
        {
            return em.HasComponent<T>(entity);
        }

        /// <summary>
        /// Adds a tag component if not already there, the content of component is its empty default because we assume it is just a "tag" anyways.
        /// </summary>
        public static void AddTag<T>(Entity entity) where T : struct, IComponentData, ITag => AddTag<T>(entity, default(T));

        /// <summary>
        /// Adds a tag component if not already there. 
        /// </summary>
        public static void AddTag<T>(Entity entity, T tagContent) where T : struct, IComponentData, ITag
        {
            if (em.HasComponent<T>(entity) == false)
            {
                em.AddComponentData<T>(entity, tagContent);
            }
            else
            {
                //You can change tag content if it is already there.
                em.SetComponentData<T>(entity, tagContent);
            }
        }

        /// <summary>
        /// Removes a tag component if it is there.
        /// </summary>
        public static void RemoveTag<T>(Entity entity) where T : struct, IComponentData, ITag
        {
            if (em.HasComponent<T>(entity))
            {
                em.RemoveComponent<T>(entity);
            }
        }

        public static void Issue<ReactiveComponent,ReactiveGroup>()
        where ReactiveComponent : struct, IReactive
        where ReactiveGroup : struct, IReactiveGroup
        => em.Issue<ReactiveComponent, ReactiveGroup>();

        public static void Issue<ReactiveComponent, ReactiveGroup>(ReactiveComponent rx)
        where ReactiveComponent: struct, IReactive
        where ReactiveGroup : struct, IReactiveGroup
        => em.Issue<ReactiveComponent, ReactiveGroup>(rx);

        /// <summary>
        /// Runs on the currently active world and EntityManager.
        /// </summary>
        public static T GetComponentData<T>(Entity entity) where T : struct, IComponentData
        {
            //return em.GetComponentData<T>(entity);
            return em.GetComponentData<T>(entity);
        }

        /// <summary>
        /// Runs on the currently active world and EntityManager.
        /// </summary>
        public static void SetComponentData<T>(Entity entity, T data) where T : struct, IComponentData
        {
            em.SetComponentData<T>(entity, data);
        }
    }
}