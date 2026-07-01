# TODO
- [ ]: In NavMenu.razor add an additional line to the following if any are hidden from the leftnav because of permission ie like 3 modules loaded </br> visible  <div class="nav__foot">@Catalog.Modules.Count module@(Catalog.Modules.Count == 1 ? "" : "s") loaded</div>
      -- or something else to indicate that x are loaded but you can see/only see y so its not saying 3 if the leftnav has 1 item in it as is the case for testuser or anon users
- [ ]: Integrate Microsoft Test Platform w/ xUnit
- [ ]: Deploy to Azure using as much IAC as possible (as long as it makes sense)
      - [ ]: PREREQUISITE: Setup Azure account with limits and learn how to bring up and down to keep costs minimal
      - [ ]: MAYBE: Hook up CI/CD with Github and demo deploying a full feature slice aka module and service app
- [ ]: Add a Customer Support agent chatbot with MFA & Integrate with Azure AI Foundry 