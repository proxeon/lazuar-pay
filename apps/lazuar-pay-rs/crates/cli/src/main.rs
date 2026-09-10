//! `lazuar-pay` entry. Host JSON to stdout (`--compact` / `--quiet`); problems to stderr.

use clap::Parser;
use pay_cli::{run, stdout_json, Cli};

#[tokio::main]
async fn main() {
    let cli = Cli::parse();
    let compact = cli.compact;
    let quiet = cli.quiet;
    match run(cli).await {
        Ok(body) => match stdout_json(&body, compact, quiet) {
            Some(s) => println!("{s}"),
            None => {}
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
