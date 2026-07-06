# TODO

- 1: NUMBER ONE: FIX SSH ISSUES B4 INTERVIEW (CURRENTLY NEED TO FORWARD LIKE EVERY PORT)!

- REVIEW SEMANTIC LAYER CODE FROM WORK. DESIGN BASIC EXAMPLE HERE, CONSIDER HOW YOU WOULD HAVE MULTIPLE SERVICES CONTRIBUTE/HANDLE MULTIPLE TEAM
- BIG BRAIN TIME:
            - CREATE ENDPOINTS TO RETURN DATA GRIDS, PAGINATED ETC
            - SAME COMPOSABILITY GOES INTO SEMANTIC LAYER AND CHAT
            - SEE VERCEL FOR WHATEVER THEY CALL DYNAMICALLY BUILDING UI ELEMENTS IE CHARTS ETC FROM AGENT OUTPUTS

- Estimate scalability vs alternative architecthures
      - How is each service deployed on cloud?
      - Consider how you would refactor heavily trafficed Services out of the monolith
            - Think of simmilar such challenges with claude

-- Make a build pipeline that runs tests with playwright and passes/fails CI/CD? Would look amazing in demo.

-- NICE TO HAVE: Shapedivider and more nicely style landing page
      - More realsitic app structure

- DO i need to organize Atrium.Design (cuz theres A lotta stuff in root)?

- Review local model choices/selection

- Setup Postman/Bruno/Scalar/Other endpoint testing

- Setup a way to look at the live DB DATABASE like Beekeeper Studio, SSMS, etc

- FIX DARK MODE BUTTON HOVER CONTRAST BUG

- EXPLAIN:
      - " // Stash the access token as a claim so the Blazor circuit (which has no HttpContext) can attach
        // it to outbound API calls. Dev-simple; production would use a token store with refresh
        // (e.g. Duende.AccessTokenManagement) rather than parking the token in the principal."

- EXPLAIN:
      - Portal has its own endpoints? Peraps I need a super deep dive into portal

- DEEP DIVES NEEDED:
      - STRUCTURED LOGGING SETUP--THE WHAT N THE WHY

- FIX CHAT 'Wheres my order' returns nothing

- FIX BROKEN CHAT FEEDBACK HANDS

- Review/Audit Error Handling in Services layers!

- Should Test Projects be split out by Module/Service instead of all in one? Seems to violate the whole deployment flexibility model we were working on?

- LOW:
      - Add a SQL formatter to enforce consistency and compliment prettier

- UI Polish
      - Side Nav Menu -- Need to brainstorm different ideas
      - Support Modal (says support 2x in a row)
            - BUG: each new chat message makes the window grow until its at max size and scrollable
- AI Chat
      - BASIC RAG a BASIC Semantic layer/authz query composition/evaluator layer that wont even pass unauthozired queries to be built
            - Would help to demonstrate the RLS architecure ive talked about
            - Could demonstrate basic ass rag with RRF done in the app layer 
- Host (Azure)
      - How would I host and deploy to Azure?
            -Be able to defend decisions

- Atrium Architecture
      - Learn this inside and out. Flows, happy paths, how things break, how their registered, how to add a new module and or service
            - MAKE A CHECKLIST to ensure youi dont miss anything
            - NOTE: This setup is almost word for word the architcture (minus the AI stuff) of Cozen so I should know this inside and out

- Figure out how to exclude directories and filetypes from VSCode search

