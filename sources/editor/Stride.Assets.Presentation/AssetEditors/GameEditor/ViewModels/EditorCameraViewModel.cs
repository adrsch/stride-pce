// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Commands;
using Stride.Core.Presentation.ViewModel;
using Stride.Assets.Presentation.AssetEditors.GameEditor.Services;
using Stride.Assets.Presentation.SceneEditor;
using Stride.Engine.Processors;
using Stride.Core.Yaml.Tokens;

namespace Stride.Assets.Presentation.AssetEditors.GameEditor.ViewModels
{
    public class EditorCameraViewModel : DispatcherViewModel
    {
        private readonly IEditorGameController controller;
        
        private bool orthographicProjection;
        private float orthographicSize;
        private float nearPlane;
        private float farPlane;
        private float fieldOfView;

        public static float[] AvailableMovementSpeed =
        {
            0f,
            0.03f,
            0.05f,
            0.1f,
            1f,
        };

        private static int FindValidMoveSpeedIndex(float value)
        {
            for (int i = 0; i < AvailableMovementSpeed.Length; i++)
            {
                // Assume in order
                if (MathUtil.NearEqual(value, AvailableMovementSpeed[i]) || AvailableMovementSpeed[i] > value)
                    return i;
            }
            return AvailableMovementSpeed.Length - 1;
        }

        private static float FindValidMoveSpeedValue(int index)
        {
            return AvailableMovementSpeed[MathUtil.Clamp(index, 0, AvailableMovementSpeed.Length - 1)];
        }

        private float FindValidMoveSpeedForPercent(float percent)
        {
            return MathUtil.Lerp(MinCameraSpeed, MaxCameraSpeed, MathUtil.Clamp(percent, 0, 1));
        }

        private float FindMoveSpeedPercent(float speed)
        {
            return (speed - MinCameraSpeed) / (MaxCameraSpeed - MinCameraSpeed);
        }

        private void SetMinSpeed(float minSpeed)
        {
            var currentPercent = FindMoveSpeedPercent(MoveSpeed);
            Service.MinCameraSpeed = minSpeed;
            MoveSpeedPercent = currentPercent;
        }

        private void SetMaxSpeed(float maxSpeed)
        {
            var currentPercent = FindMoveSpeedPercent(MoveSpeed);
            Service.MaxCameraSpeed = maxSpeed;
            MoveSpeedPercent = currentPercent;
        }

        public EditorCameraViewModel([NotNull] IViewModelServiceProvider serviceProvider, [NotNull] IEditorGameController controller)
            : base(serviceProvider)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            this.controller = controller;
            ResetCameraCommand = new AnonymousCommand(ServiceProvider, () => Service.ResetCamera());
            ResetCameraOrientationCommand = new AnonymousCommand<CameraOrientation>(ServiceProvider, value => Service.ResetCameraOrientation(value));
        }

        public bool OrthographicProjection { get { return orthographicProjection; } set { SetValue(OrthographicProjection != value, () => Service.SetOrthographicProjection(orthographicProjection = value)); } }

        public float OrthographicSize { get { return orthographicSize; } set { SetValue(Math.Abs(OrthographicSize - value) > MathUtil.ZeroTolerance, () => Service.SetOrthographicSize(orthographicSize = value)); } }

        public float NearPlane { get { return nearPlane; } set { SetValue(Math.Abs(NearPlane - value) > MathUtil.ZeroTolerance, () => Service.SetNearPlane(nearPlane = value)); } }

        public float FarPlane { get { return farPlane; } set { SetValue(Math.Abs(FarPlane - value) > MathUtil.ZeroTolerance, () => Service.SetFarPlane(farPlane = value)); } }

        public float FieldOfView { get { return fieldOfView; } set { SetValue(Math.Abs(FieldOfView - value) > MathUtil.ZeroTolerance, () => Service.SetFieldOfView(fieldOfView = value)); } }

        public float MoveSpeed { get { return Service.MoveSpeed; } set { SetValue(Math.Abs(MoveSpeed - value) > MathUtil.ZeroTolerance, () => Service.MoveSpeed = value); } }

        public float MinCameraSpeed { get { return Service.MinCameraSpeed; } set { SetValue(Math.Abs(MinCameraSpeed - value) > MathUtil.ZeroTolerance, () => SetMinSpeed(value)); } }

        public float MaxCameraSpeed { get { return Service.MaxCameraSpeed; } set { SetValue(Math.Abs(MaxCameraSpeed - value) > MathUtil.ZeroTolerance, () => SetMaxSpeed(value)); } }

        public float MoveSpeedPercent { get { return FindMoveSpeedPercent(MoveSpeed); } set { SetValue(!MathUtil.NearEqual(value, MoveSpeedPercent), () => MoveSpeed = FindValidMoveSpeedForPercent(value)); } }

        public int MoveSpeedIndex { get { return FindValidMoveSpeedIndex(MoveSpeed); } set { SetValue(value != MoveSpeedIndex, () => MoveSpeed = FindValidMoveSpeedValue(value)); } }

        public float SceneUnit { get { return Service.SceneUnit; } set { SetValue(Math.Abs(SceneUnit - value) > MathUtil.ZeroTolerance, () => Service.SceneUnit = value); } }

        public ICommandBase ResetCameraCommand { get; }

        public ICommandBase ResetCameraOrientationCommand { get; }

        private IEditorGameCameraViewModelService Service => controller.GetService<IEditorGameCameraViewModelService>();

        public void LoadSettings([NotNull] SceneSettingsData sceneSettings)
        {
            Service.LoadSettings(sceneSettings);
            OrthographicProjection = sceneSettings.CamProjection == CameraProjectionMode.Orthographic;
            OrthographicSize = sceneSettings.CamOrthographicSize;
            NearPlane = sceneSettings.CamNearClipPlane;
            FarPlane = sceneSettings.CamFarClipPlane;
            FieldOfView = sceneSettings.CamVerticalFieldOfView;
            SceneUnit = sceneSettings.SceneUnit <= MathUtil.ZeroTolerance ? 1.0f : sceneSettings.SceneUnit;
            MoveSpeed = sceneSettings.CamMoveSpeed;
        }

        public void SaveSettings([NotNull] SceneSettingsData sceneSettings)
        {
            Service.SaveSettings(sceneSettings);
            sceneSettings.CamProjection = OrthographicProjection ? CameraProjectionMode.Orthographic : CameraProjectionMode.Perspective;
            sceneSettings.CamOrthographicSize = OrthographicSize;
            sceneSettings.CamNearClipPlane = NearPlane;
            sceneSettings.CamFarClipPlane = FarPlane;
            sceneSettings.CamVerticalFieldOfView = FieldOfView;
            sceneSettings.SceneUnit = SceneUnit;
            sceneSettings.CamMoveSpeed = MoveSpeed;
        }

        public void IncreaseMovementSpeed()
        {
            var currentSpeedIndex = FindValidMoveSpeedIndex(MoveSpeed);
            MoveSpeed = FindValidMoveSpeedValue(currentSpeedIndex + 1);
        }

        public void DecreaseMovementSpeed()
        {
            var currentSpeedIndex = FindValidMoveSpeedIndex(MoveSpeed);
            MoveSpeed = FindValidMoveSpeedValue(currentSpeedIndex - 1);
        }
    }
}
