# Hunt Route 0.3.1

- Replace rigid 1200/900/300 m limits with estimated time savings including cast/loading and the final flight.
- Require a direct flight of at least 30 s, saving at least 10 s and 20%, and price no higher than 200 gil.
- Initial teleport allowance is 18 s. Completed plugin teleports update the session estimate to observed request-to-ready duration plus 3 s for remounting.
- Show estimated flight time, time with teleport and saved seconds. Revalidate profitability and price on Yes.
- Cover the Amh Araeng gnome case: 1094 m direct and 440 m from Mord Souq qualifies with the default allowance.

Estimates use horizontal distance and assumed 20 m/s flight speed; terrain, altitude and future loading delays can differ. Native dialog and actual teleport behavior require an in-game check.
