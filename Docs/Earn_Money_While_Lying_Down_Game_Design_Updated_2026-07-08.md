# Earn Money While Lying Down

## One-Line Overview

An idle trading management simulation set in a fantasy world, where trading cities are structured as nodes and the player operates multiple caravans while repeatedly trading goods, generating profit, and developing both the caravans and the hub city.

## Genre

Idle Game, Trading Management Simulation

**Document updated:** 2026-07-08  
**Save-system update scope:** First-build save stability and auto-save policy; second-build HMAC integrity protection

---

## First Build Goals

The first build is intended to verify whether the game's core trading loop and economic structure can provide genuine enjoyment and a tangible sense of progression.

Rather than merely listing features, the goal is to complete a Vertical Slice in which players can understand the purpose of their next action and the results of their growth even during a short play session.

### 1) Validate the Core Gameplay Loop

The player must be able to repeatedly complete the following flow through actual gameplay.

```text
Prepare a caravan in the hub city
→ Select a trading city and trade route
→ Select trade goods, food, wagon, draft animals, and mercenaries
→ Depart on a trade journey
→ Encounter events based on distance and travel time
→ Arrive at the destination city
→ Settle profits and combat results
→ Develop the player, caravan, and hub city
→ Prepare for the next trade journey
```

#### Minimum Implementation Scope

* 1 hub city
* At least 3 trading cities
* 1 operable caravan
* Buying and selling trade goods
* Loading food
* Selecting a wagon and draft animals
* Hiring mercenaries
* Selecting a trade route
* Travel time progression
* Trade route events
* Profit and loss settlement
* Purchasing progression upgrades
* Repeatable trade journeys

#### Success Criteria

* The player can complete one full cycle from trade preparation to settlement.
* The available choices for the next trade journey change depending on the previous result.
* The player can feel differences in speed, carrying capacity, profit, or risk before and after progression.
* The player can confirm at least one progression result within a short play session of approximately 10 minutes.

---

### 2) Item Data and City-Specific Product Generation Structure

Trade goods and city product lineups are managed through a data-driven structure, and each city's available products are refreshed at regular intervals.

#### Minimum `TradeItemSO` Data

* Item ID
* Item name
* Base price
* Rarity
* Weight
* Maximum generated quantity
* Icon
* Item description
* Item category
* Seasonal price modifier
* Disaster price modifier

#### City-Specific Product Generation Structure

* General products are randomly selected from the complete trade goods list, while products appropriate to each city's characteristics and surrounding environment are assigned directly.
* The quantity of each selected product is determined without exceeding that product's maximum generated quantity.
* Each city's specialty products are managed in a separate list from general products.
* Specialty products always appear in their associated city or have a high probability of appearing.
* After a set period, the existing product list is cleared and generated again.
* Product lineups must be restorable after reconnecting through the same random seed or saved data.

#### Success Criteria

* New items can be added without modifying code.
* Each city generates a different product lineup.
* The generation rules for specialty products and general products are separated.
* The product refresh cycle functions correctly.
* Product information remains consistent after saving and loading.

---

### 3) Seasons, Disasters, and Market Price Fluctuation

Seasons change as in-game time passes, and seasons and disasters affect product prices and trade results.

#### Minimum Implementation Scope

* In-game date and time
* At least 2 seasons

  * Summer
  * Winter
* At least 2 disasters

  * Drought
  * Flood
* Seasonal product price modifiers
* Disaster-based product price modifiers
* City-specific inventory changes
* Price changes caused by trends or rumors
* Reduced selling prices caused by oversupply

#### Example Profit Calculation

```text
Final Profit
= Base Sales Margin
× Distance Modifier
× Seasonal Modifier
× Disaster Modifier
× City Event Modifier
× Player Sales Markup
× Oversupply Modifier
- Food Cost
- Mercenary Hiring Cost
- Wagon Maintenance Loss
```

#### Success Criteria

* The same product has different prices depending on the season and disaster state.
* The cause of each price change can be checked through the UI.
* Repeatedly selling only one product triggers an oversupply penalty.
* Seasons and disasters affect actual decision-making rather than serving only as visual presentation.

---

### 4) Distance-Based Trade Routes and Event System

Longer trade routes provide more possible events and greater rewards.

#### Minimum Implementation Scope

* Distance data between cities
* Base travel time for each trade route
* Target event counts for each distance range
* 2 city events
* 2 trade route events
* Trade route risk level
* Distance modifiers applied to trade completion rewards

#### Example Distance-Based Event Rules

```text
Short Route: 0–1 events
Medium Route: 1–2 events
Long Route: 2–4 events
```

#### Success Criteria

* Longer trade routes provide more event opportunities and greater risk.
* Increased rewards for longer distances are balanced against the possibility of loss.
* The player can choose between a safe short route and a dangerous long route.

---

### 5) Trading City Donation System

The player can donate funds to individual trading cities, and accumulated donations change the state of the city and its trade routes.

#### Minimum Scope for the First Build

* Accumulated donations for each city
* Limits on available donation amounts
* Donation increases and decreases
* 1 positive city event based on donations
* 1 negative city event based on donations
* 1 positive trade route event based on donations
* 1 negative trade route event based on donations
* 1 hidden trade good unlocked by a donation condition
* Display of city-specific contribution or development level

#### Success Criteria

* Changes to a city or trade route are clearly visible before and after a donation.
* Donations are connected to profit, risk reduction, or product unlocks rather than functioning only as a currency sink.
* Choosing not to donate remains a strategically valid option.
* Donation results are explained through the UI, logs, or city presentation.

---

### 6) Raid Events and Mercenary System

Raid event outcomes are determined using the combat power ratio between mercenaries and monsters.

#### Minimum Implementation Scope

* Mercenary combat power stat
* Monster combat power stat
* Mercenary hiring cost
* Number of mercenary contract uses
* Defense success probability based on combat power ratio
* Loss of trade goods when combat fails
* Reduced wagon durability when combat fails
* Reduced remaining mercenary contract uses
* Possibility of losing a mercenary after combat failure
* Combat result settlement UI

#### Example Combat Resolution

```text
Defense Ratio = Mercenary Combat Power / Monster Combat Power
```

* When mercenary combat power is higher than monster combat power, the probability of successful defense increases.
* When mercenary combat power is insufficient, the player may lose trade goods, wagon durability, or the mercenary.
* Combat results display the reasons for success or failure.

#### Success Criteria

* Whether mercenaries are hired and their grade affect raid outcomes.
* The player can compare mercenary costs against expected losses.
* Combat failure does not immediately end the game and instead causes recoverable losses.

---

### 7) Food and Trade Failure System

Food functions as a core resource that determines how far a caravan can travel and whether the trade journey succeeds.

#### Minimum Implementation Scope

* Estimated food consumption by distance
* Food consumption based on caravan composition
* Loaded food quantity
* Food shortage warning
* Reduced movement speed during food shortages
* Trade failure when food remains insufficient for a specified duration
* Rechecking travel conditions when draft animals are lost in combat
* Reduced speed or trade failure when the wagon lacks the minimum required number of draft animals

#### Trade Failure Flow

```text
Normal Travel
→ Food Shortage or Insufficient Draft Animals
→ Reduced Movement Speed
→ Problem Is Not Resolved Within the Time Limit
→ Trade Failure
→ Settle Losses and Return to the Hub City
```

#### Success Criteria

* The estimated food requirement can be checked before departure.
* Loading too little food creates meaningful risk.
* Loading too much food reduces the cargo space available for trade goods.
* Food selection functions as a clear risk-versus-reward decision.

---

### 8) Wagon, Draft Animal, and Durability Systems

Wagons and draft animals affect caravan speed, carrying capacity, travel range, and failure risk.

#### Minimum Implementation Scope

* At least 2 types of draft animals
* At least 2 types of wagons
* Carrying capacity for each wagon
* Movement speed modifier for each wagon
* Minimum required draft animals for each wagon
* Speed or distance modifiers based on the number of draft animals
* Wagon durability
* Durability reduction based on completed trade journeys
* Additional durability loss after combat failure
* Wagon restriction or replacement when durability is depleted

#### Success Criteria

* Trade strategies change depending on the selected wagon and draft animals.
* Better wagons enable long-distance trading rather than merely increasing numerical values.
* Durability creates a recurring currency sink and replacement demand.

---

### 9) Settlement System

When a trade journey succeeds or fails, the player must be able to review profit, losses, combat, donations, and progression results on a single screen.

#### Minimum Implementation Scope

* Purchase cost
* Sales revenue
* Base sales margin
* Distance modifier
* Seasonal and disaster modifiers
* City event modifier
* Mercenary hiring cost
* Food cost
* Lost trade goods
* Combat result
* Wagon durability loss
* Player progression experience or progression currency
* Final net profit

#### Success Criteria

* The player can understand why a profit or loss occurred.
* The player can immediately proceed to the next progression action from the settlement screen.
* Settlement entries display both numerical values and the reasons for increases or decreases.

---

### 10) Detailed Progression System

Progression is divided into four areas: player, caravan, hub city, and trading city.

#### Player Progression

* Purchase discount rate
* Sales markup rate
* Reduced loss after trade failure
* Gradual or exponential increases in upgrade costs
* Optional random choices for selected progression categories

#### Caravan Progression

* Movement speed
* Maximum carrying capacity
* Food efficiency
* Number of operable caravans
* Mercenary slots or maximum hireable grade

#### Hub City Progression

* Mercenary combat power
* Available wagon tiers
* Maximum number of draft animals
* Number of operable caravans
* Development facility unlocks

#### Trading City Progression

* Contribution
* Development level
* Rumor occurrence probability
* Maximum donation amount
* Hidden trade goods
* City event probability

#### Success Criteria

* Each progression category strengthens different functions.
* The player can choose which area to invest in first.
* Rising progression costs suppress late-game currency inflation.

---
### 11) Dual-Currency Structure and Inflation Control

Currency used for trading is separated from currency used to develop the hub city, clarifying each progression objective.

#### Currency Types

* Trading currency

  * Purchasing products
  * Purchasing food
  * Hiring mercenaries
  * Purchasing wagons and draft animals
  * Donating to cities
* Hub city development currency

  * Upgrading hub facilities
  * Unlocking wagon tiers
  * Increasing the maximum number of draft animals
  * Increasing mercenary combat power
  * Increasing the number of operable caravans

#### Development Currency Acquisition

* Rewards from a trading city's development level or contribution
* Delivery of specified trade goods
* City event rewards
* Long-distance trade rewards
* Hidden trade good transaction rewards

#### Inflation Control Measures

* Exponential increases in progression costs
* City donations
* Hiring and rehiring mercenaries
* Purchasing and replacing wagons
* Wagon durability
* Investment in unexplored regions
* Losses from failed trade journeys

#### Success Criteria

* The purposes of the two currencies are clearly separated.
* Trading currency does not accumulate indefinitely and is continuously consumed.
* Development currency functions as a long-term progression objective.

---

### 12) Investment and Unexplored Region Unlocks

Instead of automatically unlocking cities through city-to-city connections, the player invests in unexplored regions to discover new trading cities or trade routes.

#### Minimum Scope for the First Build

* 1 locked unexplored region
* Investment cost
* Investment progress
* Unlocking a new city or trade route when investment is completed
* Currency consumption through investment
* New products or higher-profit opportunities after the unlock

#### Success Criteria

* Investment functions as a mid- to late-game currency sink.
* Unlocking a new region provides a clear long-term goal.
* The unlocked region offers products or risks that differ from existing regions.

---

### 13) Loan and Bankruptcy Recovery System

Loans function as a safety mechanism that prevents players from becoming unable to continue after a failed trade journey.

#### Minimum Scope for the First Build

* Loan eligibility conditions
* Loan principal
* Repayment amount
* Repayment deadline or automatic repayment percentage
* Restrictions on additional loans
* Guaranteed minimum funds
* Bankruptcy state detection
* Provision of a basic wagon or minimum trading capital

#### Success Criteria

* The player can begin trading again after a failed trade journey.
* Loans cannot be exploited as an unlimited source of duplicated funds.
* Taking a loan has a cost, such as interest, reduced revenue, or functional restrictions.

---

### 14) Offline Progression, Saving, and Loading

The first build verifies time calculation, save-file integrity, and data recovery, which are core technical risks in an idle game.

#### Minimum Implementation Scope

* Save trade start time
* Save expected trade completion time
* Save game exit time
* Save each caravan's progression state
* Save each city's product list
* Save season and disaster states
* Save city donations and development levels
* Save wagon durability
* Save loan state
* JSON-based loading
* Calculate elapsed offline time
* Automatically settle completed trade journeys

#### Save Stability and Recovery

* Do not overwrite the current main save file directly.
* Write new save data to a temporary file first.
* Validate the temporary save before replacing the main save.
* Preserve the previous valid main save as a backup file.
* Store the save-data version, save sequence number, and save timestamp.
* On startup, inspect the main, temporary, and backup save files.
* Load the newest valid candidate according to its save sequence number.
* Do not immediately delete a corrupted save or overwrite it with default data.
* Offer a new-game transition only when all available save candidates are invalid.

#### Auto-Save Policy

* Mark save data as changed when persistent gameplay state is modified.
* Check for dirty save data every 10 seconds and save only when changes exist.
* Save immediately when a critical state is confirmed, including trade departure, settlement completion, progression purchases, donations, investments, and loans.
* Do not save immediately for temporary UI selections, previews, or unconfirmed inputs.
* Coalesce repeated save requests into a single operation.
* Prevent two save operations from writing at the same time.
* Attempt to save when the application is paused or closed normally, but do not rely only on shutdown callbacks.

#### Success Criteria

* Progress is restored after closing and relaunching the game.
* Trade completion during offline time is determined accurately.
* The progression states of multiple caravans can be saved individually.
* A valid main, temporary, or backup save can be selected and restored after an interrupted write.
* Corrupted save files are preserved for diagnosis and do not automatically erase the last valid progress.
* Auto-save does not write repeatedly when no persistent state has changed.
* Critical state transitions are saved without waiting for the regular 10-second interval.
* Basic defensive handling exists for system time changes or abnormal termination.

---

### 15) Programmatic Scene Structure

Scene transitions and initialization order are clearly separated to reduce dependencies between systems.

#### Basic Scene Flow

```text
Boot
→ Title
→ Loading
→ InGame
→ Title
```

#### Minimum Implementation Scope

* `BootScene`

  * Initialize shared managers
  * Load saved data
  * Determine the initial scene
* `TitleScene`

  * New Game
  * Continue
  * Options
  * Exit
* `LoadingScene`

  * Asynchronous scene loading
  * Progress display
  * Data initialization
* `InGameScene`

  * Hub city
  * World map
  * Trading city screen
  * May be divided into multiple in-game scenes when necessary
* Return to the title from in-game
* Prevent duplicate input during scene transitions

#### Success Criteria

* The responsibility of each scene is clearly separated.
* New Game and Continue are distinguished based on whether saved data exists.
* Shared managers are not created more than once when additional in-game scenes are added.

---

### 16) Early-Game Tutorial

The tutorial is structured around completing the first trade journey directly rather than presenting a sequence of explanation windows.

#### Example Tutorial Flow

```text
Introduction: Inherit the Caravan Business
→ Purchase the First Trade Goods
→ Load Food
→ Select a Wagon and Mercenaries
→ Depart for a Nearby City
→ Experience the First Event
→ Complete the First Settlement
→ Purchase the First Upgrade
```

#### Minimum Implementation Scope

* Introduction to the character and world setting
* Guided first trade journey
* Highlighting required UI elements
* Protective balancing to prevent failure during the first trade journey
* First-settlement bonus
* First progression choice
* Tutorial skip or replay confirmation

#### Success Criteria

* The player can complete the first trade journey without reading a separate manual.
* The player experiences the core loop once before the tutorial ends.
* The tutorial transitions naturally into the normal game flow.

---

### 17) Character Concept and Mascot

The player character is a person exhausted by office life who unexpectedly inherits a caravan business and begins developing it.

#### Player Character Concept

* Appearance of an office worker in their 30s or 40s
* Tired but realistic facial expression
* A novice caravan master who unexpectedly inherited the business
* A character who learns trading and management alongside the player
* Clothing or posture may change as the character progresses

#### Functional Role of the Mascot

* Tutorial guidance
* Trade completion notifications
* Risk warnings
* Reactions to settlement results
* Explanations of cities and products
* Brief feedback on repetitive player actions
* Reinforcement of the game's comedic tone and setting

#### First Build Goals

* Define the mascot's role
* Finalize one basic design direction
* Create basic expressions or poses
* Include the mascot in the tutorial and settlement screen
* Create a minimum set of notification dialogue

---

### 18) Low-Fatigue UX and Tangible Progression

The game is designed to reduce the fatigue of repetition and allow players to confirm meaningful results even during short sessions.

#### Minimum Implementation Scope

* Save frequently used trade configurations
* Load the previous trade configuration
* Display recommended trade goods
* Display expected profit and expected risk
* Minimize the number of clicks required to depart
* Batch settlement of completed trade journeys
* Notifications for available progression upgrades
* Emphasize key values in settlement results
* Provide at least one reward or progression opportunity during a short session
* Minimize unnecessary confirmation pop-ups

#### Success Criteria

* Preparing a repeated trade journey does not take excessively long.
* The player can easily understand the current objective and recommended action.
* The player can settle profits and perform a progression action shortly after logging in.
* Progress continues without requiring extended play sessions.

---

### 19) First Build Completion Criteria

The first build is considered complete when all of the following conditions are met.

* The core trading loop can be played from beginning to end.
* Items and city products are generated through a data-driven structure.
* A season or disaster affects prices and profits.
* Event counts and rewards differ by route distance.
* A city or trade route changes according to donation amounts.
* Raid outcomes are determined using mercenary and monster combat power.
* Food and wagon durability affect the possibility of trade failure.
* Profit and losses can be reviewed on the settlement screen.
* Progression can be felt in at least two of the following areas: player, caravan, and hub city.
* Trading currency and development currency are used for different purposes.
* One new region can be unlocked through investment in an unexplored area.
* The player can recover from bankruptcy through a loan or minimum-funds guarantee.
* Trade progression is restored after the game is closed.
* Interrupted or corrupted saves can recover from the newest valid main, temporary, or backup file.
* Auto-save records critical confirmed state changes and avoids unnecessary writes when no persistent data has changed.
* The Boot → Title → Loading → InGame → Title flow functions correctly.
* The first trade journey can be completed through the tutorial.
* The mascot is used in the tutorial or settlement screen.
* Progression or rewards can be felt even during a short play session.

---

## Second Build Goals

The second build expands the core systems validated in the first build and develops the content production pipeline and long-term progression structure.

### 1) Expand the Trading City Donation System

* Expand city-specific donation tiers
* Add positive and negative city events
* Add positive and negative trade route events
* Further divide contribution and development levels
* Add visual changes to cities
* Expand shop inventories based on donations
* Add hidden trade goods
* Increase the maximum donation amount through progression
* Differentiate donation efficiency by city
* Balance development currency earned through donations

---

### 2) Expand Unexplored Region Expeditions and Investment

* Reduce reliance on a linear city-to-city unlock structure
* Add unexplored expedition destinations
* Add expedition investment stages
* Add random events during investment
* Add investment failure or delay
* Require delivery of trade goods for investment
* Unlock new cities when investment is completed
* Unlock new trade routes when investment is completed
* Unlock unique products when investment is completed
* Balance the system as a long-term currency sink

---

### 3) Expand the Loan System

* Multiple loan products
* Loan limits
* Credit rating or repayment history
* Automatic repayment
* Interest rates
* Late-payment penalties
* Restrictions on additional loans
* Limits on bankruptcy protection uses
* Balance the minimum-funds guarantee
* Display loan usage on the settlement screen
* Rules preventing loan exploitation

---

### 4) Expand Content

* Add trading cities
* Differentiate regional environments and products
* Add trade goods
* Add specialty products
* Add seasons
* Add disasters
* Add city events
* Add trade route events
* Add raid events
* Add monster types
* Add mercenary types and grades
* Add wagon types
* Add draft animal types
* Add hidden trade goods
* Add post-tutorial scenarios
* Add mascot dialogue and presentation

---

### 5) Progression and Economy Balancing

* Balance the player's purchase discount rate
* Balance the player's sales markup rate
* Adjust caravan speed and carrying-capacity progression curves
* Adjust hub city upgrade costs
* Adjust trading city donation costs
* Adjust mercenary hiring and rehiring costs
* Adjust costs by wagon tier
* Adjust wagon durability loss
* Adjust the exponential rate of progression cost increases
* Adjust losses from failed trade journeys
* Adjust acquisition and consumption rates for the dual-currency structure
* Verify late-game inflation

---

### 6) Convenience Features and UX Expansion

* Operate multiple caravans
* Settle multiple trade journeys simultaneously
* Trade configuration presets
* Recommended trade routes
* Recommended products
* Expected profit comparison
* Expected risk comparison
* Review an automatic redeparture option
* City and product search
* Event history
* Economic change logs
* Tutorial replay
* Mascot notification settings

---

### 7) Save Data Protection

* Apply an HMAC-based integrity check to detect save-data modification.
* Verify the HMAC before accepting and deserializing the protected payload.
* Treat an HMAC mismatch as an invalid save candidate and continue recovery using another valid candidate when available.
* Do not store the HMAC secret as a plain-text string literal in the game code.
* Obtain or construct the secret through a platform-secure storage mechanism, a build-time injection process, or another separated key-management layer appropriate to the target platform.
* Keep save protection separate from JSON serialization and physical file storage.
* Preserve compatibility with the first-build main, temporary, and backup recovery flow.
* Document that client-side HMAC raises the cost of casual save editing but does not provide absolute protection against reverse engineering.

#### Success Criteria

* Modified payloads fail integrity verification.
* Valid protected saves continue to load through the existing recovery flow.
* The serializer, HMAC protector, validator, and file-storage responsibilities can be tested independently.
* The HMAC secret is not committed as a readable plain-text literal in the source repository.

---

## Deferred and Lower-Priority Features

The following features will be reevaluated for workload and impact after the core gameplay has been validated.

* Reduced idle-game penalties

  * Adjust only when offline progression is excessively disadvantageous compared with online progression
* Slave system

  * Requires review of ethical presentation and compatibility with the world setting
  * May make the combat outcome system unnecessarily complex
* Mid- to late-game transportation expansion

  * Ships
  * Large wagons
  * Flying transportation
* Customization of purchasable animal types
* Wagon component customization
* Time-based mercenary contracts
* Wagon repair system
* Final mascot design

  * Proposal 1: Anthropomorphic crow character
  * Proposal 2: Strange cat-like character

---
## Detailed Feature Overview

### 1) Core Gameplay

* Prepare a caravan in the hub city.
* Select a trading city and trade route.
* Configure products, food, wagon, draft animals, and mercenaries.
* Dispatch the caravan.
* Resolve events that occur during travel.
* Settle profits and losses after arrival.
* Use acquired currency to strengthen the next trade journey.

---

### 2) Multiple Caravan Operation

* The player can initially operate 1 caravan.
* Increase the maximum number of caravans through hub city progression.
* Manage each caravan's origin, destination, products, food, speed, and carrying capacity.
* Save each caravan's trade start time and expected completion time.
* Separate events and settlement results for each caravan.
* Batch-settle the completed results of multiple caravans.

---

### 3) Item Data

* Item ID
* Name
* Base price
* Rarity
* Weight
* Maximum quantity
* Icon
* Description
* Category
* Seasonal modifier
* Disaster modifier
* Availability by city
* Specialty product status

---

### 4) City-Specific Product Generation

* Randomly select general products from the complete product list.
* Generate inventory within each product's maximum quantity.
* Generate specialty products from a separate list.
* Manage each city's product refresh time.
* Restore inventory and prices through saved data.
* Increase product slots according to the city's development level.

---

### 5) Market Prices

* Calculate purchase and selling prices based on the base price.
* Product prices change according to the season.
* Disasters change the price and quantity of specific products.
* City events change prices.
* Trends and rumors increase demand for specific products.
* Repeated sales apply an oversupply penalty.
* Profit modifiers are applied according to distance and risk.

---

### 6) Seasons and Disasters

* Change seasons according to the in-game date.
* Use product modifier tables for each season.
* Maintain disasters such as droughts and floods for a set duration.
* Disasters affect product prices, inventory, and event probability.
* Display season and disaster states on the world map and city UI.

---

### 7) Trade Routes and Distance

* Manage cities as nodes.
* Trade routes connect two cities.
* Each trade route has distance, travel time, risk, and an event list.
* Longer distances increase the target number of events.
* Higher distance and risk increase expected profit.
* Locked trade routes are unlocked through investment or city development.

---

### 8) City Donations

* Save accumulated donations for each city.
* Set an upper limit on donation amounts.
* Change city events according to donation totals.
* Change trade route events according to donation totals.
* Unlock hidden products when donations reach specified thresholds.
* Events may reduce donations or development levels.
* Present city development results through UI and visuals.

---

### 9) City Contribution and Development Level

* Contribution represents how much the player has contributed to a city.
* Development level represents the city's overall state of growth.
* Contribution increases through donations, trading, and event resolution.
* Development level increases through contribution or investment results.
* Product count, product grade, and maximum donations increase with development level.
* Contribution and development are used for rumor probability and hidden product unlocks.

---

### 10) City Events

* Positive events provide price discounts, increased inventory, rare product appearances, and similar benefits.
* Negative events cause higher prices, reduced inventory, lower donation totals, and similar penalties.
* Events occur according to city development, season, disaster, and donations.
* Display each event's remaining duration.
* Restore prices and inventory to their normal state after an event ends.

---

### 11) Trade Route Events

* Raids
* Blocked roads
* Encounters with traveling merchants
* Discovery of lost cargo
* Severe weather
* Discovery of shortcuts
* Safety events based on city donation status
* Risk events based on disaster status
* Event count adjustment by distance
* Record event outcomes in settlement data

---

### 12) Raids and Combat

* Mercenaries and monsters have combat power values.
* Calculate defense success probability using the combat power ratio.
* Successful combat protects cargo and the wagon.
* Failed combat may cause the loss of products, wagon durability, or mercenaries.
* Mercenaries have a limited number of contract uses.
* Mercenaries must be rehired after their contract uses are depleted.
* Mercenaries may be lost after combat failure.
* Explain combat outcomes on the settlement screen.

---

### 13) Mercenaries

* Purchase mercenaries
* Mercenary combat power
* Mercenary grade
* Mercenary hiring cost
* Number of contract uses
* Disappearance probability after combat failure
* Unlock higher-grade mercenaries through hub city progression
* Assign mercenaries to individual caravans
* Expand mercenary slots

---

### 14) Wagons

* Wagon tier
* Maximum carrying capacity
* Base movement speed
* Minimum number of draft animals
* Durability
* Durability reduction after completing a trade journey
* Additional durability loss after combat failure
* Usage restriction when durability is depleted
* Unlock higher tiers through hub city progression

---

### 15) Draft Animals

* Draft animal type
* Pulling power of each animal
* Movement speed modifier
* Food consumption
* Minimum quantity required by each wagon
* Loss after combat failure
* Maximum operational quantity limited by the hub city
* Assignment to individual caravans

---

### 16) Food

* Calculate expected consumption using trade distance and caravan composition.
* Food consumes wagon carrying capacity.
* Movement speed decreases when food is insufficient.
* Trade fails when food shortages continue.
* Loading more food reduces the carrying capacity available for products.
* Display the recommended amount of food before departure.

---

### 17) Trade Failure

* Food shortage
* Insufficient number of draft animals
* Depleted wagon durability
* Inability to continue after a raid
* Loss of remaining products after failure
* Reduced wagon durability after failure
* Return to the hub city after failure
* Recovery through minimum funds or a loan

---

### 18) Settlement

* Product purchase cost
* Product sales revenue
* Distance modifier
* Seasonal modifier
* Disaster modifier
* Event modifier
* Player discounts and markups
* Food cost
* Mercenary cost
* Product loss
* Wagon durability loss
* Final net profit
* Development currency earned

---

### 19) Player Progression

* Purchase discount rate
* Sales markup rate
* Reduced losses after trade failure
* Progression choices
* Rising progression costs
* Selected randomized progression choices
* Player level or reputation

---

### 20) Caravan Progression

* Movement speed
* Maximum carrying capacity
* Food efficiency
* Mercenary slots
* Number of operable caravans
* Access to long-distance trade
* Automated trade support features

---

### 21) Hub City Progression

* Mercenary combat power
* Wagon tiers
* Maximum number of draft animals
* Maximum number of operable caravans
* Development facilities
* Use of development currency
* New feature unlocks
* Long-term progression objectives

---

### 22) Trading City Progression

* Contribution
* Development level
* Rumor probability
* Maximum donation amount
* Product slots
* Rare product appearance probability
* Hidden products
* Expanded city events

---

### 23) Dual-Currency Structure

* Trading currency is used for products and trade preparation.
* Development currency is used for hub city progression.
* Development currency is acquired from trading cities.
* The two currencies support different progression objectives.
* When necessary, they may be exchanged at a restricted rate.

---

### 24) Investment in Unexplored Regions

* Select a locked region.
* Check the required currency and products.
* Invest a specified amount.
* Accumulate investment progress.
* Unlock a city or trade route when investment is completed.
* New regions provide new products and risks.

---

### 25) Loans and Bankruptcy Recovery

* Detect bankruptcy conditions.
* Calculate the available loan amount.
* Guarantee minimum trading capital.
* Repay through interest or revenue deductions.
* Restrict consecutive loans.
* Guarantee a basic wagon or the funds needed to purchase basic products.

---

### 26) Offline Progression

* Save trade start and completion times.
* Compare them with the current time when the player reconnects.
* Move completed trade journeys into a pending settlement state.
* Display the remaining time for ongoing trade journeys.
* An upper limit may be applied to offline progression time.
* Include basic defensive logic against system time manipulation.

---

### 27) Saving and Loading

#### Persistent Gameplay Data

* Player currency
* Development currency
* Caravan states
* City product inventories
* Seasons and disasters
* City donations
* City development levels
* Investment progress
* Wagons and draft animals
* Mercenaries
* Loans
* Tutorial progress
* Option settings

#### Save Metadata

* Save-data version
* Monotonically increasing save sequence number
* UTC save timestamp
* Payload validation information

#### Save Stability and Recovery

* Write new data to a temporary save instead of overwriting the main save directly.
* Validate the temporary save before promotion.
* Preserve the previous valid main save as a backup.
* Inspect the main, temporary, and backup candidates during loading.
* Select the newest valid candidate by save sequence number.
* Preserve corrupted candidates for diagnosis rather than immediately deleting or overwriting them.
* Request a new game only when every available candidate is invalid.

#### Auto-Save Behavior

* Use a dirty flag so unchanged data is not written repeatedly.
* Check for pending changes every 10 seconds.
* Save immediately after confirmed critical state transitions.
* Exclude temporary UI selections and previews from immediate saves.
* Merge repeated save requests and serialize only one write operation at a time.
* Attempt a final save on pause and normal shutdown without treating those callbacks as the only protection against data loss.

#### Second-Build Protection

* Add HMAC-based save-data modification detection.
* Keep HMAC protection separate from serialization and file storage.
* Do not store the HMAC secret as a plain-text string literal in source code.

---

### 28) Scene Structure

* Initialize shared systems in Boot.
* Provide New Game and Continue in Title.
* Handle asynchronous scene loading in Loading.
* Conduct actual trading and progression in InGame.
* If necessary, separate the hub city, world map, and trading city into individual in-game scenes.
* Save and return to the title from in-game.

---

### 29) Tutorial

* Present the introduction in which the player inherits the caravan business.
* Guide the first product purchase.
* Guide food loading.
* Guide wagon and mercenary selection.
* Dispatch the first trade journey.
* Let the player experience the first event.
* Complete the first settlement and progression purchase.
* Provide tutorial skip and replay options.

---

### 30) Character and Mascot

* The player is a novice caravan master exhausted by office life.
* The player character expresses emotional changes throughout the progression process.
* The mascot handles guidance, warnings, notifications, and settlement reactions.
* The mascot reinforces the game's comedic atmosphere.
* The mascot's final appearance is determined according to the production status of the first build.

---

### 31) Low-Fatigue UX

* Load the previous trade configuration
* Trade presets
* Recommended products
* Expected profit
* Expected risk
* Batch settlement
* Notifications for available progression
* Redeparture with minimal clicks
* Prioritize key information
* Minimize unnecessary pop-ups
* Provide progression rewards during short sessions

---
## Risks and Alternatives

### 1) Excessive Scope for the First Build

#### Risk

Including the economy, combat, donations, investment, loans, tutorial, and scene structure in the first build may result in a workload closer to a Beta build.

#### Alternative

Limit each system to the minimum scope required for one meaningful validation.

* 3 cities
* Approximately 5 products
* 2 seasons
* 2 disasters
* 2 city events
* 2 trade route events
* 1 raid event
* 2 mercenary types
* 2 wagon types
* 2 draft animal types
* 1 hidden product
* 1 unexplored region unlock
* 1 loan type
* 1 tutorial trade journey

---

### 2) Increasing Economic System Complexity

#### Risk

When season, disaster, distance, donations, oversupply, and progression stats are all applied to prices simultaneously, the result may be difficult to understand.

#### Alternative

* Fix the order of price calculations.
* Display every modifier on the settlement screen.
* Limit modifier ranges in the initial build.
* Combine overlapping modifiers into a single group.

---

### 3) Confusion Caused by the Dual-Currency Structure

#### Risk

If trading currency and development currency have overlapping uses, players may struggle to understand the difference between them.

#### Alternative

* Use distinct colors and icons for each currency.
* Display acquisition sources and uses in the UI.
* Do not require both currencies for the same function.
* Restrict development currency usage to the hub city in the first build.

---

### 4) Insufficient Impact of the Donation System

#### Risk

If donations only change probability values, players may not feel their effects.

#### Alternative

* Product unlocks
* Price discounts
* Increased inventory
* Reduced event frequency
* Visual city changes
* Display donation effects on the settlement screen

---

### 5) Loan System Exploitation

#### Risk

Players may repeatedly take loans to secure funds without meaningful losses.

#### Alternative

* Loan limits
* Limit the player to 1 active loan
* Automatic repayment
* Interest
* Reduced sales revenue for a set period
* Limits on consecutive bankruptcy protection

---

### 6) Frustration Caused by Randomized Progression

#### Risk

If essential progression options appear only at random, progression may be blocked or choice-related stress may increase.

#### Alternative

* Provide essential progression categories as fixed purchases.
* Use randomized choices only for secondary progression.
* Allow the player to select a desired option after a specified number of attempts.

---

### 7) Excessive Losses from Raid Outcomes

#### Risk

If the player loses both mercenaries and a wagon at the same time, recovery may become excessively difficult.

#### Alternative

* Set a maximum loss limit
* Protect the player from the first failure
* Preserve a minimum amount of products
* Provide a basic wagon
* Guarantee a loan or minimum funds
* Prevent raid failure during the tutorial section

---

### 8) Overengineering the Programmatic Scene Structure

#### Risk

Dividing the in-game experience into multiple scenes too early may complicate data transfer and loading architecture.

#### Alternative

Use a single `InGameScene` in the first build and switch between screens and states within it.
Separate the hub city, world map, and trading city into different scenes only when their content becomes sufficiently large.

---

### 9) Delays in Mascot and Character Production

#### Risk

Character design and animation production may delay development of the core systems.

#### Alternative

* Use static images and expression swaps in the first build.
* Minimize mascot animation.
* Prioritize use in the tutorial and settlement screen.
* Finalize the design after system validation.

---

### 10) Offline Time Manipulation

#### Risk

Players may change the system clock to complete trade journeys instantly or repeatedly obtain rewards.

#### Alternative

* Detect times earlier than the last saved time
* Limit the maximum duration of offline rewards
* Log abnormal time changes
* Expand to server time or external time verification when necessary

---

### 11) Save Corruption and Excessive Auto-Save Writes

#### Risk

Directly overwriting one JSON file can destroy the latest valid progress if the application closes during the write. Saving every input immediately can also cause unnecessary disk writes, overlapping operations, and partially confirmed gameplay states to be persisted.

#### Alternative

* Use temporary, main, and backup save candidates.
* Validate a temporary save before replacing the main save.
* Store a save sequence number and load the newest valid candidate.
* Use a dirty flag and write only when persistent state has changed.
* Save critical confirmed state transitions immediately.
* Coalesce repeated non-critical save requests.
* Allow only one file-write operation at a time.
* Preserve invalid files for diagnosis and prompt for a new game only when all candidates fail validation.
