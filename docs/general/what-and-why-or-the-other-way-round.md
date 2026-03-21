## why
I do like writing code, i really do, or do I... When I think about coding I come to a conclusion that it's not about writing the code at all. it's about solving problems, architecting elegant solutions, making them evolve with time, and above the other, delivering business value (however one defines a business in given context fkorz). It's just that coding is an emanation of it, hence, I guess, I would happily repeat the common statement - i do like coding, i really do :)

Anyway, with the advent of LLMs, AI assisted coding became the reality. 
It's not the future, it's not even happening now, it's already happened. Coding has been solved. the most common programming language is... English, or any other natural language for that matter.

That may still be a bold statement as of early 2026, but I am quite concerned that's the way it is and there is no coming back. This is what folks at Anthropic or OpenAI do - they create apps without manually writing the code; the number of tools, IDEs and a flood of agents and skills available in the wild confirm that.

A production ready application without writing a line of code may still require some expert knowledge, but I strongly believe that programming does not differ much from communicating in any other language (as in human spoken language) - one needs to know what's to be said and how to appropriately express oneself. One's final product - be it an essay or a software for that matter - may be elegant or not so much;  if it delivers what's expected of it though - some functionality or perhaps a clearly stated chain of thoughts and perhaps a reasonable conclussion - then bingo, one managed to express one's intent successfully. 

When it is so easy to produce software, it stops being a valuable, self defending asset itself. Reverse engineering can be quick and cost effective without sacrificing the quality; the software becomes just qanother project artifact. It is still a vessel required to deliver a value to its users of course, although it pretty much becomes a terminal to an idea, an interface to a story one tells, a value one promises. But hey, wasn't it always the point after all? 


## what
YAF... Yet Another Framework... right...

I guess every dev at some point decides to put together an own framework of some sort:
* specific utils used every now and then
* some more sophisticated app building helpers, templates
* etc

Sometimes such frameworks get traction, and become popular, sometimes not and they die in silence. quite often, i believe, they just make an internal toolset used by freelancers and companies to automate repetetive app setups / boiler plates / scaffoldings for different projects; allowing dev team to work in a familiar environment and not have to waste time on context switching when jumping between projects is also a good reason. When a framework is the same, one can focus on the business aspect of software. That makes things a bit more straight forward, or at least that's the common reasoning behind the idea of an own framework.

The point of this excercise though is not a framework itself, but the way it is created - no code is to be written by a human, AI will generate it entirely; the only content allowed to be created by a human is the documentation, readmes, plans, tools, skills. Responsibility for all the decisions made (automatically or after a carefeul consideration) are on a human. 
AI is the vehicle, a human is the driver.

Having the above in mind, i decided to create Yet Another Framework - i guess the name is not very original, but hey, that's an experiment after all. If it is usable, then i'll happily use it in my own projects. If it's not then i would like to learn what went wrong and most likely repeat the excercise: **do -> fail -> learn -> repeat** - agentic loops are not the only ones allowed here... :) 


### basic drivers
* a simple framework in modern .net to be used as a starting point for business applications
* it should use the modern software concepts such as DDD, Clean Architecture, IoC with DI, scalability, continerization, cloud readines, etc
* all the high level decisions should be documented in a form of ADRs
* it should have a reasonable test coverage
* it should have ci/cd pipelines 
* it should be delivered as a nuget package, its setup in a project should be as simple as possible
* there should be a basic usage example in a form of a web api application
* it should be created with AI tooling
* the code is to be documented and open sourced since the beginning
* the process should be documented, so one can verify it and/or repeat it at own discretion


## how - a ruleset
* any ai tool, agent can be used for the job, although ClaudeCode is a tool of choice here. This does not mean GitHub copilot will not be used, it's just a matter of preference
* any plugin or skill can be used
* strictly **NO code** should be written by a human, while all the responsibility is on a human - if there are bugs - be it code or architectural - they are to be blamed on a human
* human interaction is allowed for installation of tools, plugins, etc, when a tool has issues handling them on its own
* human can create tools - agents, skills, although the code/prompts should still be generated by LLMs

## dev AI-mighty?
![AImighty](resources/aimighty.gif)