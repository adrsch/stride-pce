﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BepuPhysicIntegrationTest.Integration.Components.Colliders;
using BepuPhysicIntegrationTest.Integration.Components.Containers;
using BepuPhysicIntegrationTest.Integration.Configurations;
using BepuPhysics;
using BepuPhysics.Collidables;
using Stride.Core.Annotations;
using Stride.Engine;
using Stride.Engine.Design;
using Stride.Games;

namespace BepuPhysicIntegrationTest.Integration.Processors
{
    public class ContainerProcessor : EntityProcessor<ContainerComponent>
    {
        private BepuConfiguration _bepuConfiguration = new();

        public ContainerProcessor()
        {
            Order = 10000;
        }

        protected override void OnSystemAdd()
        {
            var configService = Services.GetService<IGameSettingsService>();
            _bepuConfiguration = configService.Settings.Configurations.Get<BepuConfiguration>();
            if (_bepuConfiguration == null)
            {
                _bepuConfiguration = new BepuConfiguration();
                _bepuConfiguration.BepuSimulations.Add(new BepuSimulation());
            }

            Services.AddService(_bepuConfiguration);
        }

        protected override void OnEntityComponentAdding(Entity entity, [NotNull] ContainerComponent component, [NotNull] ContainerComponent data)
        {
            component.ContainerData = new(component, _bepuConfiguration);
            component.ContainerData.BuildOrUpdateContainer();
        }
        protected override void OnEntityComponentRemoved(Entity entity, [NotNull] ContainerComponent component, [NotNull] ContainerComponent data)
        {
            component.ContainerData?.DestroyContainer();
            component.ContainerData = null;
        }

        public override void Update(GameTime time)
        {
            var dt = (float)time.Elapsed.TotalMilliseconds;
            if (dt == 0f)
                return;

            //var totalWatch = new Stopwatch();
            //var simUpdWatch = new Stopwatch();
            //var simStepWatch = new Stopwatch();
            //var parForWatch = new Stopwatch();

            //totalWatch.Start();
            //Debug.WriteLine($"Start");

            foreach (var bepuSim in _bepuConfiguration.BepuSimulations)
            {
                if (!bepuSim.Enabled)
                    continue;

                var SimTimeStep = dt * bepuSim.TimeWarp; //Calculate the theoretical time step of the simulation
                bepuSim.RemainingUpdateTime += SimTimeStep; //Add it to the counter

                var realSimTimeStepInSec = (bepuSim.RemainingUpdateTime - (bepuSim.RemainingUpdateTime % bepuSim.SimulationFixedStep)) / 1000f; //Calculate the real time step of the simulation
                realSimTimeStepInSec = Math.Min(realSimTimeStepInSec, (bepuSim.MaxStepPerFrame * bepuSim.SimulationFixedStep) / 1000);
                //Debug.WriteLine($"    SimTimeStepSinceLastFrame : {SimTimeStep}\n    realSimTimeStep : {realSimTimeStepInSec*1000}");


                //simUpdWatch.Start();
                bepuSim.CallSimulationUpdate(realSimTimeStepInSec); //cal the SimulationUpdate with the real step time of the sim in secs
                //simUpdWatch.Stop();

                //simStepWatch.Start();
                int stepCount = 0;
                while (bepuSim.RemainingUpdateTime >= bepuSim.SimulationFixedStep & stepCount < bepuSim.MaxStepPerFrame)
                {
                    bepuSim.Simulation.Timestep(bepuSim.SimulationFixedStep / 1000f, bepuSim.ThreadDispatcher); //perform physic simulation using bepuSim.SimulationFixedStep
                    bepuSim.RemainingUpdateTime -= bepuSim.SimulationFixedStep;
                    stepCount++;
                }
                //simStepWatch.Stop();

                //parForWatch.Start();
                if (bepuSim.ParallelUpdate)
                {
                    var a = Parallel.For(0, bepuSim.Simulation.Bodies.ActiveSet.Count, (i) =>
                    {
                        var handle = bepuSim.Simulation.Bodies.ActiveSet.IndexToHandle[i];
                        var entity = bepuSim.Bodies[handle];
                        var body = bepuSim.Simulation.Bodies[handle];

                        var entityTransform = entity.Transform;
                        entityTransform.Position = body.Pose.Position.ToStrideVector();
                        entityTransform.Rotation = body.Pose.Orientation.ToStrideQuaternion();
                        entityTransform.UpdateWorldMatrix();
                    });
                }
                else
                {
                    for (int i = 0; i < bepuSim.Simulation.Bodies.ActiveSet.Count; i++)
                    {
                        var handle = bepuSim.Simulation.Bodies.ActiveSet.IndexToHandle[i];
                        var entity = bepuSim.Bodies[handle];
                        var body = bepuSim.Simulation.Bodies[handle];

                        var entityTransform = entity.Transform;
                        entityTransform.Position = body.Pose.Position.ToStrideVector();
                        entityTransform.Rotation = body.Pose.Orientation.ToStrideQuaternion();
                        entityTransform.UpdateWorldMatrix();
                    }
                }
                //parForWatch.Stop();
                //Debug.WriteLine($"    stepCount : {stepCount}\n    SimulationFixedStep : {bepuSim.SimulationFixedStep}\n    RemainingUpdateTime : {bepuSim.RemainingUpdateTime}");
            }
            //totalWatch.Stop();
            //Debug.WriteLine($"-   Sim update function call : {simUpdWatch.ElapsedMilliseconds}\n-   Sim timestep : {simStepWatch.ElapsedMilliseconds}\n-   Position update : {parForWatch.ElapsedMilliseconds}\nEnd in : {totalWatch.ElapsedMilliseconds}");
            base.Update(time);
        }

    }

    internal class ContainerData
    {
        internal ContainerComponent ContainerComponent { get; }
        internal BepuConfiguration BepuConfiguration { get; }
        internal BepuSimulation BepuSimulation => BepuConfiguration.BepuSimulations[ContainerComponent.SimulationIndex];

        internal bool isStatic { get; set; } = false;

        internal BodyInertia ShapeInertia { get; set; }
        internal TypedIndex ShapeIndex { get; set; }

        internal BodyDescription BDescription { get; set; }
        internal BodyHandle BHandle { get; set; } = new(-1);

        internal StaticDescription SDescription { get; set; }
        internal StaticHandle SHandle { get; set; } = new(-1);

        public bool Exist => isStatic ? BepuSimulation.Simulation.Statics.StaticExists(SHandle) : BepuSimulation.Simulation.Bodies.BodyExists(BHandle);


        public ContainerData(ContainerComponent containerComponent, BepuConfiguration bepuConfiguration)
        {
            ContainerComponent = containerComponent;
            BepuConfiguration = bepuConfiguration;
        }

        internal void BuildOrUpdateContainer()
        {
            if (BepuSimulation == null)
                throw new Exception("A container must be inside a BepuSimulation.");

            if (ShapeIndex.Exists)
                BepuSimulation.Simulation.Shapes.Remove(ShapeIndex);

            var colliders = ContainerComponent.Entity.GetAll<ColliderComponent>();

            if (colliders.Count() == 0)
            {
                return;
            }
            else if (colliders.Count() == 1)
            {
                switch (colliders.First())
                {
                    case BoxColliderComponent box:
                        var shapeB = new Box(box.Size.X, box.Size.Y, box.Size.Z);
                        ShapeInertia = shapeB.ComputeInertia(box.Mass);
                        ShapeIndex = BepuSimulation.Simulation.Shapes.Add(shapeB);
                        break;
                    case SphereColliderComponent sphere:
                        var shapeS = new Sphere(sphere.Radius);
                        ShapeInertia = shapeS.ComputeInertia(sphere.Mass);
                        ShapeIndex = BepuSimulation.Simulation.Shapes.Add(shapeS);
                        break;
                    case CapsuleColliderComponent capsule:
                        var shapeC = new Capsule(capsule.Radius, capsule.Length);
                        ShapeInertia = shapeC.ComputeInertia(capsule.Mass);
                        ShapeIndex = BepuSimulation.Simulation.Shapes.Add(shapeC);
                        break;
                    case ConvexHullColliderComponent convexHull: //TODO
                        var shapeCh = new ConvexHull();
                        ShapeInertia = shapeCh.ComputeInertia(convexHull.Mass);
                        ShapeIndex = BepuSimulation.Simulation.Shapes.Add(shapeCh);
                        break;
                    case CylinderColliderComponent cylinder:
                        var shapeCy = new Cylinder(cylinder.Radius, cylinder.Length);
                        ShapeInertia = shapeCy.ComputeInertia(cylinder.Mass);
                        ShapeIndex = BepuSimulation.Simulation.Shapes.Add(shapeCy);
                        break;
                    case TriangleColliderComponent triangle:
                        var shapeT = new Triangle(triangle.A.ToNumericVector(), triangle.B.ToNumericVector(), triangle.C.ToNumericVector());
                        ShapeInertia = shapeT.ComputeInertia(triangle.Mass);
                        ShapeIndex = BepuSimulation.Simulation.Shapes.Add(shapeT);
                        break;
                    default:
                        throw new Exception("Unknown Shape");
                }
            }
            else
            {
                BepuUtilities.Memory.Buffer<CompoundChild> compoundChildren;
                BodyInertia shapeInertia;
                Vector3 shapeCenter;

                using (var compoundBuilder = new CompoundBuilder(BepuSimulation.BufferPool, BepuSimulation.Simulation.Shapes, colliders.Count()))
                {
                    foreach (var collider in colliders)
                    {
                        switch (collider)
                        {
                            case BoxColliderComponent box:
                                compoundBuilder.Add(new Box(box.Size.X, box.Size.Y, box.Size.Z), collider.Entity.Transform.ToBepuPose(), collider.Mass);
                                break;
                            case SphereColliderComponent sphere:
                                compoundBuilder.Add(new Sphere(sphere.Radius), collider.Entity.Transform.ToBepuPose(), collider.Mass);
                                break;
                            case CapsuleColliderComponent capsule:
                                compoundBuilder.Add(new Capsule(capsule.Radius, capsule.Length), collider.Entity.Transform.ToBepuPose(), collider.Mass);
                                break;
                            case ConvexHullColliderComponent convexHull: //TODO
                                compoundBuilder.Add(new ConvexHull(), collider.Entity.Transform.ToBepuPose(), collider.Mass);
                                break;
                            case CylinderColliderComponent cylinder:
                                compoundBuilder.Add(new Cylinder(cylinder.Radius, cylinder.Length), collider.Entity.Transform.ToBepuPose(), collider.Mass);
                                break;
                            case TriangleColliderComponent triangle:
                                compoundBuilder.Add(new Triangle(triangle.A.ToNumericVector(), triangle.B.ToNumericVector(), triangle.C.ToNumericVector()), collider.Entity.Transform.ToBepuPose(), collider.Mass);
                                break;
                            default:
                                throw new Exception("Unknown Shape");
                        }
                    }

                    compoundBuilder.BuildDynamicCompound(out compoundChildren, out shapeInertia, out shapeCenter);
                }

                ShapeInertia = ShapeInertia;
                ShapeIndex = BepuSimulation.Simulation.Shapes.Add(new Compound(compoundChildren));
            }

            if (ShapeInertia.InverseMass == float.PositiveInfinity) //TODO : don't compute inertia (up) if kinematic or static
                ShapeInertia = new BodyInertia();

            var pose = ContainerComponent.Entity.Transform.ToBepuPose();
            switch (ContainerComponent)
            {
                case BodyContainerComponent _c:
                    isStatic = false;
                    if (_c.Kinematic)
                    {
                        ShapeInertia = new BodyInertia();
                    }

                    BDescription = BodyDescription.CreateDynamic(pose, ShapeInertia, ShapeIndex, .1f);

                    if (BHandle.Value != -1)
                    {
                        BepuSimulation.Simulation.Bodies.ApplyDescription(BHandle, BDescription);
                    }
                    else
                    {
                        BHandle = BepuSimulation.Simulation.Bodies.Add(BDescription);
                        BepuSimulation.Bodies.Add(BHandle, ContainerComponent.Entity);
                    }
                  
                    break;
                case StaticContainerComponent _c:
                    isStatic = true;

                    SDescription = new StaticDescription(pose, ShapeIndex);

                    if (SHandle.Value != -1)
                    {
                        BepuSimulation.Simulation.Statics.ApplyDescription(SHandle, SDescription);
                    }
                    else
                    {
                        SHandle = BepuSimulation.Simulation.Statics.Add(SDescription);
                        BepuSimulation.Statics.Add(SHandle, ContainerComponent.Entity);
                    }

                    break;
                default:
                    break;
            }
        }
        internal void DestroyContainer()
        {
            if (BHandle.Value != -1 && BepuSimulation.Simulation.Bodies.BodyExists(BHandle))
            {
                BepuSimulation.Simulation.Bodies.Remove(BHandle);
                BepuSimulation.Bodies.Remove(BHandle);
            }

            if (SHandle.Value != -1 && BepuSimulation.Simulation.Statics.StaticExists(SHandle))
            {
                BepuSimulation.Simulation.Statics.Remove(SHandle);
                BepuSimulation.Statics.Remove(SHandle);
            }

            if (ShapeIndex.Exists)
                BepuSimulation.Simulation.Shapes.Remove(ShapeIndex);
        }
    }

}
