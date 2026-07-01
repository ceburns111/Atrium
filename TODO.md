# TODO
- [ ]: In NavMenu.razor add an additional line to the following if any are hidden from the leftnav because of permission ie like 3 modules loaded </br> visible  <div class="nav__foot">@Catalog.Modules.Count module@(Catalog.Modules.Count == 1 ? "" : "s") loaded</div>
      -- or something else to indicate that x are loaded but you can see/only see y so its not saying 3 if the leftnav has 1 item in it as is the case for testuser or anon users
- [ ]: [DISCUSS FIRST] Deploy to Azure using as much IAC as possible (as long as it makes sense)
      - [ ]: PREREQUISITE: Setup Azure account with limits and learn how to bring up and down to keep costs minimal
            - Is there any way to test IAC type configs that could be deployed to azure locally
            - Can we discuss different ways I could deploy this and pros cons (ie cost, availablity etc of each) 
      - [ ]: MAYBE: Hook up CI/CD with Github and demo deploying a full feature slice aka module and service app
- [ ]: [DISCUSS FIRST] Add a Customer Support agent chatbot with MFA & Integrate with Azure AI Foundry 
- [ ]: Integrate Microsoft Test Platform w/ xUnit
