using System.Linq;
using Content.Client.Eui;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared.Eui;
using Content.Shared.Ghost.Roles;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client.UserInterface.Systems.Ghost.Controls.Roles
{
    [UsedImplicitly]
    public sealed class GhostRolesEui : BaseEui
    {
        private readonly GhostRolesWindow _window;
        private GhostRoleRulesWindow? _windowRules = null;
        private uint _windowRulesId = 0;

        public GhostRolesEui()
        {
            _window = new GhostRolesWindow();

            _window.OnRoleRequestButtonClicked += info =>
            {
                _windowRules?.Close();

                if (info.Kind == GhostRoleKind.RaffleJoined)
                {
                    SendMessage(new LeaveGhostRoleRaffleMessage(info.Identifier));
                    return;
                }

                _windowRules = new GhostRoleRulesWindow(info.Rules, _ =>
                {
                    SendMessage(new RequestGhostRoleMessage(info.Identifier));

                    // if raffle role, close rules window on request, otherwise do
                    // old behavior of waiting for the server to close it
                    if (info.Kind != GhostRoleKind.FirstComeFirstServe)
                        _windowRules?.Close();
                });
                _windowRulesId = info.Identifier;
                _windowRules.OnClose += () =>
                {
                    _windowRules = null;
                };
                _windowRules.OpenCentered();
            };

            _window.OnRoleFollow += info =>
            {
                SendMessage(new FollowGhostRoleMessage(info.Identifier));
            };

            _window.OnClose += () =>
            {
                SendMessage(new CloseEuiMessage());
            };
        }

        public override void Opened()
        {
            base.Opened();
            _window.OpenCentered();
        }

        public override void Closed()
        {
            base.Closed();
            _window.Close();
            _windowRules?.Close();
        }

        public override void HandleState(EuiStateBase state)
        {
            base.HandleState(state);

            if (state is not GhostRolesEuiState ghostState)
                return;

            // We must save BodyVisible state, so all Collapsible boxes will not close
            // on adding new ghost role.
            // Save the current state of each Collapsible box being visible or not
            _window.SaveCollapsibleBoxesStates();

            // Clearing the container before adding new roles
            _window.ClearEntries();

            var entityManager = IoCManager.Resolve<IEntityManager>();
            var sysManager = entityManager.EntitySysManager;
            var spriteSystem = sysManager.GetEntitySystem<SpriteSystem>();
            var requirementsManager = IoCManager.Resolve<JobRequirementsManager>();

            // TODO: role.Requirements value doesn't work at all as an equality key, this must be fixed
            // Grouping roles
            var groupedRoles = ghostState.GhostRoles.GroupBy(
                role => (role.Name, role.Description, role.Requirements, role.WhitelistRequired)); //backmen: whitelist

            //start-backmen: whitelist
            var cfg = IoCManager.Resolve<Robust.Shared.Configuration.IConfigurationManager>();
            //end-backmen: whitelist

            // Add a new entry for each role group
            foreach (var group in groupedRoles)
            {
                var name = group.Key.Name;
                var description = group.Key.Description;
                bool hasAccess = true;
                FormattedMessage? reason;

                //start-backmen: whitelist
                if (
                    group.Key.WhitelistRequired &&
                    cfg.GetCVar(Shared.Backmen.CCVar.CCVars.WhitelistRolesEnabled) &&
                    !requirementsManager.IsWhitelisted()
                    )
                {
                    hasAccess = false;
                    reason = FormattedMessage.FromMarkupOrThrow(Loc.GetString("playtime-deny-reason-not-whitelisted"));
                }
                else
                //end-backmen: whitelist
                if (!requirementsManager.CheckRoleRequirements(group.Key.Requirements, null, out reason))
                {
                    hasAccess = false;
                }

                // Adding a new role
                _window.AddEntry(name, description, hasAccess, reason, group, spriteSystem);
            }

            // Restore the Collapsible box state if it is saved
            _window.RestoreCollapsibleBoxesStates();

            // Close the rules window if it is no longer needed
            var closeRulesWindow = ghostState.GhostRoles.All(role => role.Identifier != _windowRulesId);
            if (closeRulesWindow)
                _windowRules?.Close();
        }
    }
}
