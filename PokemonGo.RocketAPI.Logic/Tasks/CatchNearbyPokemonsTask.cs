﻿using POGOProtos.Map.Pokemon;
using POGOProtos.Networking.Responses;
using PokemonGo.RocketAPI.Logic.State;
using PokemonGo.RocketAPI.Logic.Utils;
using PokemonGo.RocketAPI.Logic.Logging;
using System.Linq;
using System.Threading;
using PokemonGo.RocketAPI.Logic.Event;

namespace PokemonGo.RocketAPI.Logic.Tasks
{
    public static class CatchNearbyPokemonsTask
    {
        private static IOrderedEnumerable<MapPokemon> GetNearbyPokemons(Context ctx)
        {
            var mapObjects = ctx.Client.Map.GetMapObjects().Result;

            var pokemons = mapObjects.MapCells.SelectMany(i => i.CatchablePokemons)
                    .OrderBy(i => LocationUtils.CalculateDistanceInMeters(ctx.Client.CurrentLatitude, ctx.Client.CurrentLongitude, i.Latitude, i.Longitude));

            return pokemons;
        }

        public static void Execute(Context ctx, StateMachine machine)
        {
            Logger.Write("Looking for pokemon..", LogLevel.Debug);

            var pokemons = GetNearbyPokemons(ctx);
            foreach (var pokemon in pokemons)
            {
                if (ctx.LogicSettings.UsePokemonToNotCatchFilter &&
                    ctx.LogicSettings.PokemonsNotToCatch.Contains(pokemon.PokemonId))
                {
                    Logger.Write("Skipped " + pokemon.PokemonId);
                    continue;
                }

                var distance = LocationUtils.CalculateDistanceInMeters(ctx.Client.CurrentLatitude, ctx.Client.CurrentLongitude, pokemon.Latitude, pokemon.Longitude);
                Thread.Sleep(distance > 100 ? 15000 : 500);

                var encounter = ctx.Client.Encounter.EncounterPokemon(pokemon.EncounterId, pokemon.SpawnPointId).Result;

                if (encounter.Status == EncounterResponse.Types.Status.EncounterSuccess)
                {
                    CatchPokemonTask.Execute(ctx, machine, encounter, pokemon);
                }
                else
                {
                    machine.Fire(new WarnEvent { Message = $"Encounter problem: {encounter.Status}" });
                }

                // If pokemon is not last pokemon in list, create delay between catches, else keep moving.
                if (!Equals(pokemons.ElementAtOrDefault(pokemons.Count() - 1), pokemon))
                {
                    Thread.Sleep(ctx.LogicSettings.DelayBetweenPokemonCatch);
                }
            }
        }
    }
}
