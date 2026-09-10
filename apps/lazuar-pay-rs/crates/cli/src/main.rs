//! `lazuar-pay` entry. Pretty-print host JSON to stdout; problems to stderr.

use clap::Parser;
use pay_cli::{run, Cli};

#[tokio::main]
async fn main() {
    let cli = Cli::parse();
    match run(cli).await {
        Ok(body) => match serde_json::to_string_pretty(&body) {
            Ok(s) => println!("{s}"),
            Err(e) => {
                eprintln!("{e}");
                std::process::exit(1);
            }
        },
        Err(e) => {
            // Agents parse problem+json (036/006 #8). Human Display is in `detail`.
            match serde_json::to_string(&e.to_json()) {
                Ok(s) => eprintln!("{s}"),
                Err(_) => eprintln!("{e}"),
            }
            std::process::exit(e.exit_code());
        }
    }
}
