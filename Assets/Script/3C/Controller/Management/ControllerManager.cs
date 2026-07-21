using System.Collections.Generic;

namespace CGame
{
    public class ControllerManager : IManager
    {
        private readonly List<IController> controllers = new List<IController>();
        private readonly Dictionary<IController, ControllerRegistration> registrations = new Dictionary<IController, ControllerRegistration>();

        public override int Priority => 90;

        /// <summary>
        /// 创建并注册指定类型的控制器。
        /// </summary>
        public TController CreatingController<TController>() where TController : IController, new()
        {
            return CreateController<TController>(out _);
        }

        public TController CreateController<TController>(out IControllerRegistration registration) where TController : IController, new()
        {
            var controller = new TController();
            registration = RegisterController(controller);
            return controller;
        }

        /// <summary>
        /// 注册控制器实例。
        /// </summary>
        public void RegisteringController(IController controller)
        {
            RegisterController(controller);
        }

        public IControllerRegistration RegisterController(IController controller)
        {
            if (controller == null)
            {
                return null;
            }

            if (registrations.TryGetValue(controller, out ControllerRegistration existing))
            {
                return existing;
            }

            controllers.Add(controller);
            var registration = new ControllerRegistration(this, controller);
            registrations.Add(controller, registration);
            return registration;
        }

        /// <summary>
        /// 注销控制器实例。
        /// </summary>
        public void UnregisteringController(IController controller)
        {
            if (controller == null || !registrations.TryGetValue(controller, out ControllerRegistration registration))
            {
                return;
            }

            registration.Dispose();
        }

        /// <summary>
        /// 获取第一个指定类型的控制器。
        /// </summary>
        public TController GettingController<TController>() where TController : class, IController
        {
            for (int i = 0; i < controllers.Count; i++)
            {
                if (controllers[i] is TController controller)
                {
                    return controller;
                }
            }

            return null;
        }

        /// <summary>
        /// 更新所有控制器。
        /// </summary>
        public override void Update(float elapseSeconds)
        {
            for (int i = 0; i < controllers.Count; i++)
            {
                controllers[i].UpdatingController(elapseSeconds);
            }
        }

        /// <summary>
        /// 关闭控制器管理器并清空控制器。
        /// </summary>
        public override void Shutdown()
        {
            for (int i = 0; i < controllers.Count; i++)
            {
                if (controllers[i] is Controller controller)
                {
                    controller.UnpossessingPawn();
                }
            }

            controllers.Clear();
            foreach (ControllerRegistration registration in registrations.Values)
            {
                registration.Invalidate();
            }

            registrations.Clear();
        }

        private void Release(ControllerRegistration registration, IController controller)
        {
            if (!registrations.TryGetValue(controller, out ControllerRegistration current) || current != registration)
            {
                return;
            }

            registrations.Remove(controller);
            if (controllers.Remove(controller) && controller is Controller typedController)
            {
                typedController.UnpossessingPawn();
            }
        }

        private sealed class ControllerRegistration : IControllerRegistration
        {
            private ControllerManager owner;
            private IController controller;

            public ControllerRegistration(ControllerManager owner, IController controller)
            {
                this.owner = owner;
                this.controller = controller;
            }

            public bool IsActive => owner != null && controller != null;

            public void Dispose()
            {
                ControllerManager currentOwner = owner;
                IController currentController = controller;
                owner = null;
                controller = null;
                currentOwner?.Release(this, currentController);
            }

            public void Invalidate()
            {
                owner = null;
                controller = null;
            }
        }
    }
}
