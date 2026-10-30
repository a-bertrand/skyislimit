using System;
using System.Linq;
using NUnit.Framework;

namespace Game.Combat.Tests
{
    /// <summary>
    /// Tests du moteur de combat. Dans Unity : Window > General > Test Runner > EditMode > Run All.
    /// </summary>
    public class BattleTests
    {
        private static Combatant Hero(int hp = 40, int atk = 12, int def = 6, int spd = 10) =>
            new Combatant("Héros", Team.Player, hp, atk, def, spd);

        private static Combatant Enemy(int hp = 50, int atk = 11, int def = 4, int spd = 8) =>
            new Combatant("Drone", Team.Enemy, hp, atk, def, spd);

        private static Battle StartBattle(Combatant a, Combatant b)
        {
            var battle = new Battle(new[] { a, b });
            battle.Start();
            return battle;
        }

        [Test]
        public void Le_plus_rapide_agit_en_premier()
        {
            var hero = Hero(spd: 5);
            var enemy = Enemy(spd: 9);

            var battle = StartBattle(hero, enemy);

            Assert.AreEqual(enemy, battle.CurrentActor);
            Assert.AreEqual(1, battle.Round);
        }

        [Test]
        public void Une_attaque_retire_attaque_moins_la_moitie_de_la_defense()
        {
            var hero = Hero(atk: 12);
            var enemy = Enemy(hp: 50, def: 4);
            var battle = StartBattle(hero, enemy);

            var events = battle.Execute(CombatAction.Attack(hero, enemy));

            Assert.AreEqual(40, enemy.Hp); // 12 - 4/2 = 10
            var attack = events.Single(e => e.Kind == CombatEventKind.Attacked);
            Assert.AreEqual(10, attack.Amount);
        }

        [Test]
        public void Les_degats_sont_au_minimum_de_1()
        {
            var hero = Hero(atk: 1);
            var enemy = Enemy(hp: 50, def: 30);
            var battle = StartBattle(hero, enemy);

            battle.Execute(CombatAction.Attack(hero, enemy));

            Assert.AreEqual(49, enemy.Hp);
        }

        [Test]
        public void Se_mettre_en_garde_divise_les_degats_par_deux()
        {
            var hero = Hero(hp: 40, def: 6);
            var enemy = Enemy(atk: 11);
            var battle = StartBattle(hero, enemy);

            battle.Execute(CombatAction.Defend(hero));
            battle.Execute(CombatAction.Attack(enemy, hero));

            Assert.AreEqual(36, hero.Hp); // (11 - 6/2) / 2 = 4
        }

        [Test]
        public void La_garde_prend_fin_au_debut_du_tour_suivant_du_defenseur()
        {
            var hero = Hero();
            var enemy = Enemy();
            var battle = StartBattle(hero, enemy);

            battle.Execute(CombatAction.Defend(hero));
            Assert.IsTrue(hero.IsDefending);

            battle.Execute(CombatAction.Attack(enemy, hero)); // fin de manche 1, le héros rejoue

            Assert.AreEqual(hero, battle.CurrentActor);
            Assert.AreEqual(2, battle.Round);
            Assert.IsFalse(hero.IsDefending);
        }

        [Test]
        public void Les_PV_ne_descendent_jamais_sous_zero()
        {
            var hero = Hero(atk: 100);
            var enemy = Enemy(hp: 5);
            var battle = StartBattle(hero, enemy);

            var events = battle.Execute(CombatAction.Attack(hero, enemy));

            Assert.AreEqual(0, enemy.Hp);
            Assert.AreEqual(5, events.Single(e => e.Kind == CombatEventKind.Attacked).Amount);
        }

        [Test]
        public void Le_combat_se_termine_quand_un_camp_est_hors_de_combat()
        {
            var hero = Hero(atk: 100);
            var enemy = Enemy(hp: 5);
            var battle = StartBattle(hero, enemy);

            var events = battle.Execute(CombatAction.Attack(hero, enemy));

            Assert.IsTrue(battle.IsOver);
            Assert.AreEqual(Team.Player, battle.Winner);
            Assert.IsNull(battle.CurrentActor);
            CollectionAssert.AreEqual(
                new[] { CombatEventKind.Attacked, CombatEventKind.Defeated, CombatEventKind.BattleEnded },
                events.Select(e => e.Kind).ToArray());
        }

        [Test]
        public void Un_combat_complet_avec_les_stats_par_defaut_est_gagne_par_le_heros()
        {
            var hero = Hero();
            var enemy = Enemy();
            var battle = StartBattle(hero, enemy);
            var ai = new BasicEnemyAI();

            var safety = 100;
            while (!battle.IsOver && safety-- > 0)
            {
                var actor = battle.CurrentActor;
                var action = actor == hero ? CombatAction.Attack(hero, enemy) : ai.Choose(battle, actor);
                battle.Execute(action);
            }

            Assert.AreEqual(Team.Player, battle.Winner);
            Assert.AreEqual(5, battle.Round);
            Assert.AreEqual(8, hero.Hp); // 4 coups de 8 encaissés
        }

        [Test]
        public void Agir_hors_de_son_tour_est_refuse()
        {
            var hero = Hero(spd: 10);
            var enemy = Enemy(spd: 1);
            var battle = StartBattle(hero, enemy);

            Assert.Throws<InvalidOperationException>(() => battle.Execute(CombatAction.Attack(enemy, hero)));
        }

        [Test]
        public void Attaquer_une_cible_hors_de_combat_est_refuse()
        {
            var hero = Hero(spd: 10);
            var enemy1 = Enemy(hp: 5, spd: 1);
            var enemy2 = new Combatant("Drone 2", Team.Enemy, 50, 11, 4, 2);
            var battle = new Battle(new[] { hero, enemy1, enemy2 });
            battle.Start();

            battle.Execute(CombatAction.Attack(hero, enemy1));   // enemy1 KO
            battle.Execute(CombatAction.Defend(enemy2));          // puis enemy1, KO, passe son tour

            Assert.AreEqual(hero, battle.CurrentActor);
            Assert.Throws<InvalidOperationException>(() => battle.Execute(CombatAction.Attack(hero, enemy1)));
        }

        [Test]
        public void Un_combat_sans_ennemi_est_refuse()
        {
            Assert.Throws<ArgumentException>(() => new Battle(new[] { Hero() }));
        }

        [Test]
        public void L_IA_attaque_l_adversaire_le_plus_affaibli()
        {
            var tank = new Combatant("Tank", Team.Player, 80, 5, 10, 1);
            var fragile = new Combatant("Fragile", Team.Player, 15, 5, 2, 2);
            var enemy = Enemy(spd: 10);
            var battle = new Battle(new[] { tank, fragile, enemy });
            battle.Start();

            var action = new BasicEnemyAI().Choose(battle, enemy);

            Assert.AreEqual(ActionKind.Attack, action.Kind);
            Assert.AreEqual(fragile, action.Target);
        }
    }
}
