# Hunt Route 0.2.1

- When a zone's route is complete, immediately open a separate next-zone offer with the destination, first target, Teleport and Later buttons. It appears even when the main hunt window is closed.
- Dismissing the offer does not reopen it every second; the main-window teleport button remains available.
- If the game removes completed bill entries, retained kill progress still permits the route to advance and offer the next zone. Unfinished discarded or replaced targets still stop following.
- Teleport remains a manual action; rejected/busy requests keep the offer open for retry.

Validation: 125 logic checks, production UI layouts including the 430x210 offer, package and install-feed verification. The new offer still requires an in-game check; within-zone automatic markers were confirmed by the user on 0.2.0.
