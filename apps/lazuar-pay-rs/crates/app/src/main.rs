//! Process entry. HTTP/workers are later PRs; `domain` is the first merge (032/06 D06.6).

fn main() {
    let arg = std::env::args().nth(1).unwrap_or_else(|| "--help".into());
    match arg.as_str() {
        "--help" | "-h" | "help" => {
            println!(
                "lazuar-pay-rs — new payment host (032)\n\
                 \n\
                 Commands (not implemented yet; run `cargo test -p domain`):\n\
                   serve              api + workers + in-process watcher\n\
                   --api-only\n\
                   --worker-only\n\
                   --watcher-only     extractable chain binary\n"
            );
        }
        "serve" | "--api-only" | "--worker-only" | "--watcher-only" => {
            eprintln!("lazuar-pay-rs: {arg} is not implemented. Domain crate is P0.");
            std::process::exit(2);
        }
        other => {
            eprintln!("unknown argument: {other}");
            std::process::exit(2);
        }
    }
}
